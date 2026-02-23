using UnityEngine;

public class WebGLMockResponder : MonoBehaviour
{
    public Animator animator;

    // Optional: clear all triggers before setting a new one
    private void ResetAllTriggers()
    {
        if (animator == null) return;

        animator.ResetTrigger("Neutral");
        animator.ResetTrigger("Confused");
        animator.ResetTrigger("Nod 1");
        animator.ResetTrigger("Nod 2");
        animator.ResetTrigger("Nod 3");
        animator.ResetTrigger("Nod 4");
        animator.ResetTrigger("Head Shake 1");
        animator.ResetTrigger("Head Shake 2");
        animator.ResetTrigger("Tap Table");
        animator.ResetTrigger("Struggling");
    }

    public void PlayByCodes(int emotionCode, int motionCode, string replyText)
    {
        if (animator == null)
        {
            Debug.LogError("[WebGLMockResponder] Animator not assigned");
            return;
        }

        Debug.Log("[WebGLMockResponder] reply=" + replyText +
                  " emotion=" + emotionCode +
                  " motion=" + motionCode);

        ResetAllTriggers();

        // ----- MOTION PRIORITY -----
        // Motion code drives visible animation first
        switch (motionCode)
        {
            case 0:
                animator.SetTrigger("Neutral");
                break;

            case 1:
                animator.SetTrigger("Nod 1");
                break;

            case 2:
                animator.SetTrigger("Head Shake 1");
                break;

            case 3:
                animator.SetTrigger("Nod 2");
                break;

            case 4:
                animator.SetTrigger("Nod 3");
                break;

            case 5:
                animator.SetTrigger("Confused");
                break;

            case 6:
                animator.SetTrigger("Tap Table");
                break;

            case 7:
                animator.SetTrigger("Struggling");
                break;

            case 8:
                animator.SetTrigger("Head Shake 2");
                break;

            case 9:
                animator.SetTrigger("Nod 4");
                break;

            default:
                // Fallback using emotion if motion not mapped
                ApplyEmotionFallback(emotionCode);
                break;
        }
    }

    private void ApplyEmotionFallback(int emotionCode)
    {
        // Simple emotion fallback logic
        switch (emotionCode)
        {
            case 0:
                animator.SetTrigger("Neutral");
                break;

            case 1:
            case 2:
                animator.SetTrigger("Nod 1");
                break;

            case 3:
            case 4:
                animator.SetTrigger("Confused");
                break;

            case 5:
            case 6:
                animator.SetTrigger("Struggling");
                break;

            case 7:
            case 8:
                animator.SetTrigger("Head Shake 2");
                break;

            default:
                animator.SetTrigger("Confused");
                break;
        }
    }
}