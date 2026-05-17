using UnityEngine;

public static class StudyTaskResultSubmissionHook
{
    public static void SubmitStudyTaskResults(StudyTaskResultPayload payload)
    {
        if (payload == null)
        {
            Debug.LogWarning("[StudyTaskResultSubmissionHook] No study task payload to submit.");
            return;
        }

        string json = StudyRuntimeContext.ToJson(payload);
        Debug.Log($"[StudyTaskResultSubmissionHook] Backend endpoint not configured. Study task result payload:\n{json}");
    }
}
