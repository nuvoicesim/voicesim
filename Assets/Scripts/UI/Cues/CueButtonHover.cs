using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI.Cues
{
    public class CueButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private GameObject buttonLabel;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            buttonLabel.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            buttonLabel.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            buttonLabel.SetActive(false);
        }
    }
}
