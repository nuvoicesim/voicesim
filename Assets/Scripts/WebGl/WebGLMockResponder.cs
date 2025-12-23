using UnityEngine;

public class WebGLMockResponder : MonoBehaviour
{
    public Animator animator;

    public void OnNurseText(string text)
    {
        if (animator == null)
        {
            Debug.LogError("Animator not assigned");
            return;
        }

        Debug.Log("[MockResponder] Nurse said: " + text);

        string lower = text.ToLower();

        if (lower.Contains("hello"))
        {
            animator.SetTrigger("Nod 1");
        }
        else if (lower.Contains("no"))
        {
            animator.SetTrigger("Head Shake 1");
        }
        else
        {
            animator.SetTrigger("Confused");
        }
    }
}
