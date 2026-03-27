using UnityEngine;

public class WebGLMockResponder : MonoBehaviour
{
    public Animator animator;

    public void OnNurseText(string text)
    {
        if (animator == null)
        {
            Debug.LogError("[WebGLMockResponder] Animator not assigned");
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

    public void PlayByCodes(int emotionCode, int motionCode, string replyText)
    {
        if (animator == null)
        {
            Debug.LogError("[WebGLMockResponder] Animator not assigned");
            return;
        }

        Debug.Log("[WebGLMockResponder] reply=" + replyText + " emotion=" + emotionCode + " motion=" + motionCode);

        // Minimal mapping for demo, you can expand later
        // motion_code 1 -> Nod, 2 -> Head Shake, else -> Confused
        if (motionCode == 1)
        {
            animator.SetTrigger("Nod 1");
        }
        else if (motionCode == 2)
        {
            animator.SetTrigger("Head Shake 1");
        }
        else
        {
            animator.SetTrigger("Confused");
        }
    }
}