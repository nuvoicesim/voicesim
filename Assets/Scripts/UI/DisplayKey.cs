using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
	public class DisplayKey : MonoBehaviour
	{
        [SerializeField] private Image keyDisplay;
        [SerializeField] private Sprite blackKeySprite;
        [SerializeField] private Sprite whiteKeySprite;
        [SerializeField] private TextMeshProUGUI promptText;

		private void Start()
		{
			if (keyDisplay == null)
			{
				Debug.LogError("Key display is not assigned");
			}
            if (blackKeySprite == null)
            {
                Debug.LogError("Black key sprite is not assigned");
            }
            if (whiteKeySprite == null)
            {
                Debug.LogError("White key sprite is not assigned");
            }
            if (promptText == null)
            {
                Debug.LogError("Prompt text is not assigned");
            }
		}

        private void Update()
        {
            if (Input.GetKey(KeyCode.R))
            {
                keyDisplay.sprite = whiteKeySprite;
                promptText.text = "";
            }
            else
            {
                keyDisplay.sprite = blackKeySprite;
            }
        }
    }
}
