namespace WizardSurvivors.scripts;

// Contract for a spell-specific pulse visual that ElementalPulse drives instead of its generic
// PlaceholderShape ring. A pulse scene opts in by giving itself a child node named "Visual" whose
// script implements this interface; the visual then owns its own animation and timing, and only
// needs to be told when a pulse fires and how far it reaches.
public interface IPulseVisual
{
	// Called once per pulse. `radius` is the spell's current damage radius at this level, so the
	// effect can be drawn to the area it actually covers rather than a hardcoded size.
	void Burst(float radius);
}
