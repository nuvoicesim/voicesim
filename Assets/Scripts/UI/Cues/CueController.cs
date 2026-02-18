using UnityEngine;
using UnityEngine.UI;

namespace UI.Cues
{
	public class CueController : MonoBehaviour
	{
		/*
		 * CUE LEVEL DESCRIPTIONS
		 ***************************************************************************************************
		 * Cue level 0 (No cue yet):
		 *	- Patient Behavior: Target word not produced, only vague description or silence
		 *	- Action: Allow Semantic Cue button to be selected
		 *	
		 * Cue level 1 (Semantic Cue): 
		 *	- Condition: Student selects Semantic Cue button
		 *	- Patient Behavior: Patient still fais to retrieve word
		 *	- Action: Allow Phonemic Cue button to be selected
		 *	
		 *	Cue level 2 (Phonemic Cue): 
		 *	- Condition: Student selects Phonemic Cue button
		 *	- Patient Behavior: Patient produces phonemic approximation. Example: “cof…”, “daw…”, “sho…”
		 *	- Action: Allow Model Cue button to be selected
		 *	
		 *	Cue level 3 (Model Cue): 
		 *	- Condition: Student selects Model Cue button
		 *	- Patient Behavior: Patient repeats full word
		 *	- Action: 
		 *		- Mark target as successful
		 *		- Hide cue buttons
		 *		- Move to reinforcement phase
		 ***************************************************************************************************
		 * */
		private int currentCueLevel = 0;

		void Start()
		{
			
		}

		/*
		 * This function will take the text response from the AI request and parse it to
		 * figure out which cue should be triggered
		 * 
		 * inputs: string responseText
		 * outputs: None
		 */
		public void HandleResponse(string responseText)
		{

		}


	}
}