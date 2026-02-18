using UnityEngine;
using UnityEngine.UI;

namespace UI.Menu
{
    public class demo : MonoBehaviour
    {
        [SerializeField] private Button backButton;

        void Start()
        {
            Menu.ButtonAction(backButton, Back);
        }

        public void Back()
        {
            Debug.Log("DEMO BUTTON PRESSED");
        }

    }
}