/**************************************************************************/
/*  GodotApp.java                                                         */
/**************************************************************************/
/*                         This file is part of:                          */
/*                             GODOT ENGINE                               */
/*                        https://godotengine.org                         */
/**************************************************************************/
/* Copyright (c) 2014-present Godot Engine contributors (see AUTHORS.md). */
/* Copyright (c) 2007-2014 Juan Linietsky, Ariel Manzur.                  */
/*                                                                        */
/* Permission is hereby granted, free of charge, to any person obtaining  */
/* a copy of this software and associated documentation files (the        */
/* "Software"), to deal in the Software without restriction, including    */
/* without limitation the rights to use, copy, modify, merge, publish,    */
/* distribute, sublicense, and/or sell copies of the Software, and to     */
/* permit persons to whom the Software is furnished to do so, subject to  */
/* the following conditions:                                              */
/*                                                                        */
/* The above copyright notice and this permission notice shall be         */
/* included in all copies or substantial portions of the Software.        */
/*                                                                        */
/* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,        */
/* EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF     */
/* MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. */
/* IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY   */
/* CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,   */
/* TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE      */
/* SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.                 */
/**************************************************************************/

package com.godot.game;

import org.godotengine.godot.Godot;
import org.godotengine.godot.GodotActivity;
import org.godotengine.godot.GodotRenderView;
import org.godotengine.godot.gl.GLSurfaceView;

import android.graphics.PixelFormat;
import android.graphics.Rect;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.SurfaceHolder;

import androidx.activity.EdgeToEdge;
import androidx.core.splashscreen.SplashScreen;

import java.lang.reflect.Field;

/**
 * Template activity for Godot Android builds.
 * Feel free to extend and modify this class for your custom logic.
 */
public class GodotApp extends GodotActivity {
	static {
		// .NET libraries.
		if (BuildConfig.FLAVOR.equals("mono")) {
			try {
				Log.v("GODOT", "Loading System.Security.Cryptography.Native.Android library");
				System.loadLibrary("System.Security.Cryptography.Native.Android");
			} catch (UnsatisfiedLinkError e) {
				Log.e("GODOT", "Unable to load System.Security.Cryptography.Native.Android library");
			}
		}
	}

	private final Runnable updateWindowAppearance = () -> {
		Godot godot = getGodot();
		if (godot != null) {
			godot.enableImmersiveMode(godot.isInImmersiveMode(), true);
			godot.enableEdgeToEdge(godot.isInEdgeToEdgeMode(), true);
			godot.setSystemBarsAppearance();
		}
	};

	@Override
	public void onCreate(Bundle savedInstanceState) {
		SplashScreen.installSplashScreen(this);
		EdgeToEdge.enable(this);
		super.onCreate(savedInstanceState);
	}

	@Override
	public void onResume() {
		super.onResume();
		updateWindowAppearance.run();
		startSurfaceWatchdog();
	}

	@Override
	public void onPause() {
		stopSurfaceWatchdog();
		super.onPause();
	}

	@Override
	public void onGodotMainLoopStarted() {
		super.onGodotMainLoopStarted();
		runOnUiThread(updateWindowAppearance);
	}

	// -- Wizard Survivors: GL surface watchdog --------------------------------------------------
	// Works around a Godot 4.5.1 engine bug that leaves the game a permanent black screen (see the
	// phone section of CLAUDE.md). Godot's copy of GLSurfaceView no longer makes the UI thread wait
	// in surfaceDestroyed, so when the activity stops and restarts while the GL thread is inside
	// one long engine step (startup is a 1.5 s step), the GL thread never sees the surface go. Its
	// next swap fails on the dead surface and latches mSurfaceIsBad, with mHasSurface already true
	// again - and only a surface loss ever clears that flag, so it never draws again.
	//
	// While the activity is resumed this polls the GL thread once a second. Two polls in a row of
	// "surface bad, surface present, not paused" is the latch (a healthy swap failure clears within
	// one frame, because the real surface loss arrives right behind it). Recovery replays the
	// surface loss the GL thread missed: surfaceDestroyed, wait until the GL thread has released
	// its EGL surface and is waiting for a new one, then surfaceCreated + surfaceChanged on the
	// holder's live surface, which is exactly what switching away from the app and back does.
	//
	// The fields are private to org.godotengine.godot.gl.GLSurfaceView, so this reads them by
	// reflection. If an engine upgrade renames them, the watchdog logs once and switches itself
	// off rather than guessing. Vulkan builds have no GLSurfaceView and are skipped.
	private static final String WATCHDOG_TAG = "SurfaceWatchdog";
	private static final long WATCHDOG_INTERVAL_MS = 1000;
	private static final int WATCHDOG_LATCHED_POLLS = 2;
	private static final long RECOVERY_POLL_MS = 50;
	private static final int RECOVERY_MAX_POLLS = 40;

	private final Handler watchdogHandler = new Handler(Looper.getMainLooper());
	private boolean watchdogDisabled = false;
	private boolean watchdogResumed = false;
	private int latchedPolls = 0;
	private Field glThreadField;
	private Field surfaceIsBadField;
	private Field hasSurfaceField;
	private Field pausedField;
	private Field waitingForSurfaceField;

	private final Runnable surfaceWatchdog = new Runnable() {
		@Override
		public void run() {
			if (!watchdogResumed || watchdogDisabled) {
				return;
			}
			checkForLatchedSurface();
			watchdogHandler.postDelayed(this, WATCHDOG_INTERVAL_MS);
		}
	};

	private void startSurfaceWatchdog() {
		watchdogResumed = true;
		latchedPolls = 0;
		watchdogHandler.removeCallbacks(surfaceWatchdog);
		watchdogHandler.postDelayed(surfaceWatchdog, WATCHDOG_INTERVAL_MS);
	}

	private void stopSurfaceWatchdog() {
		watchdogResumed = false;
		watchdogHandler.removeCallbacks(surfaceWatchdog);
	}

	private GLSurfaceView findGlView() {
		Godot godot = getGodot();
		if (godot == null) {
			return null;
		}
		GodotRenderView renderView = godot.getRenderView();
		if (renderView == null || !(renderView.getView() instanceof GLSurfaceView)) {
			return null;
		}
		return (GLSurfaceView)renderView.getView();
	}

	private Object findGlThread(GLSurfaceView view) throws ReflectiveOperationException {
		if (glThreadField == null) {
			glThreadField = GLSurfaceView.class.getDeclaredField("mGLThread");
			glThreadField.setAccessible(true);
		}
		Object thread = glThreadField.get(view);
		if (thread != null && surfaceIsBadField == null) {
			Class<?> threadClass = thread.getClass();
			surfaceIsBadField = threadClass.getDeclaredField("mSurfaceIsBad");
			hasSurfaceField = threadClass.getDeclaredField("mHasSurface");
			pausedField = threadClass.getDeclaredField("mPaused");
			waitingForSurfaceField = threadClass.getDeclaredField("mWaitingForSurface");
			surfaceIsBadField.setAccessible(true);
			hasSurfaceField.setAccessible(true);
			pausedField.setAccessible(true);
			waitingForSurfaceField.setAccessible(true);
		}
		return thread;
	}

	private void checkForLatchedSurface() {
		GLSurfaceView view = findGlView();
		if (view == null) {
			return;
		}
		try {
			Object thread = findGlThread(view);
			if (thread == null) {
				return;
			}
			boolean latched = surfaceIsBadField.getBoolean(thread)
					&& hasSurfaceField.getBoolean(thread)
					&& !pausedField.getBoolean(thread);
			latchedPolls = latched ? latchedPolls + 1 : 0;
			if (latchedPolls >= WATCHDOG_LATCHED_POLLS) {
				latchedPolls = 0;
				recoverSurface(view, thread);
			}
		} catch (ReflectiveOperationException | RuntimeException e) {
			watchdogDisabled = true;
			Log.e(WATCHDOG_TAG, "GLSurfaceView internals changed; surface watchdog disabled", e);
		}
	}

	private void recoverSurface(GLSurfaceView view, Object thread) {
		Log.w(WATCHDOG_TAG, "GL surface latched bad while resumed; replaying the surface loss");
		final SurfaceHolder holder = view.getHolder();
		view.surfaceDestroyed(holder);
		watchdogHandler.postDelayed(new Runnable() {
			private int polls = 0;

			@Override
			public void run() {
				boolean released;
				try {
					released = waitingForSurfaceField.getBoolean(thread);
				} catch (IllegalAccessException e) {
					released = true;
				}
				if (!released && ++polls < RECOVERY_MAX_POLLS) {
					watchdogHandler.postDelayed(this, RECOVERY_POLL_MS);
					return;
				}
				// If the real surface went away meanwhile (the player left the app), the system's
				// own surfaceCreated will hand it back on return; do not announce a dead one.
				if (!holder.getSurface().isValid()) {
					Log.w(WATCHDOG_TAG, "surface gone during recovery; leaving it to the lifecycle");
					return;
				}
				// Hand the GL thread the holder's surface back whether or not it was seen to let
				// go, so a slow GL thread is never left without a surface by the watchdog itself.
				view.surfaceCreated(holder);
				Rect frame = holder.getSurfaceFrame();
				view.surfaceChanged(holder, PixelFormat.RGBA_8888, frame.width(), frame.height());
				Log.w(WATCHDOG_TAG, "surface handed back to the GL thread (released=" + released + ")");
			}
		}, RECOVERY_POLL_MS);
	}
	// -- end GL surface watchdog ---------------------------------------------------------------
}
