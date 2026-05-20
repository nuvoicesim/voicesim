using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace UI
{
	public class DisplayKey : MonoBehaviour
	{
        [SerializeField] private Image keyDisplay;
        [SerializeField] private Sprite blackKeySprite;
        [SerializeField] private Sprite whiteKeySprite;
        [SerializeField] private TextMeshProUGUI promptText;

        private bool IsAnyTMPInputFieldFocused()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;

            GameObject selected = es.currentSelectedGameObject;
            if (selected == null) return false;

            return selected.GetComponentInParent<TMP_InputField>() != null;
        }

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
            if (IsAnyTMPInputFieldFocused())
            {
                keyDisplay.sprite = blackKeySprite;
                return;
            }

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
