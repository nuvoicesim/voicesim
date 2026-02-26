using System.Collections;
using UnityEngine;
using TMPro;

namespace UI.Cues.WarningSystem
{
    public class WarningBanner : MonoBehaviour
    {
        [SerializeField] private GameObject root;   // Panel root
        [SerializeField] private TMP_Text text;
        [SerializeField] private float seconds = 2.5f;

        private Coroutine running;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        public void Show(string message)
        {
            if (root == null || text == null) return;

            text.text = message;
            root.SetActive(true);

            if (running != null) StopCoroutine(running);
            running = StartCoroutine(AutoHide());
        }

        private IEnumerator AutoHide()
        {
            yield return new WaitForSeconds(seconds);
            root.SetActive(false);
            running = null;
        }
    }
}
