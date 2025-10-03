using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 添加UI支持

public class CameraClipboardController : MonoBehaviour
{
    [Header("Camera References")]
    public Camera mainCamera;              // 主摄像机
    public Transform clipboardViewPosition; // clipboard查看位置

    [Header("Original Camera Settings")]
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isViewingClipboard = false;

    [Header("Animation Settings")]
    public float transitionDuration = 2f;   // 切换动画时长
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("UI References")]
    public GameObject clipboardReport;      // clipboard报告对象
    public GameObject subtitleUI;           // 字幕UI对象

    [Header("Integration")]
    public ChecklistManager checklistManager; // checklist管理器的引用

    private static CameraClipboardController instance;
    public static CameraClipboardController Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<CameraClipboardController>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("CameraClipboardController Start方法执行了");

        // 保存原始摄像机位置和旋转
        if (mainCamera == null)
            mainCamera = Camera.main;

        originalPosition = mainCamera.transform.position;
        originalRotation = mainCamera.transform.rotation;

        Debug.Log($"保存的原始摄像机位置: {originalPosition}");

        // 初始状态下隐藏clipboard报告
        if (clipboardReport != null)
            clipboardReport.SetActive(false);

        // 自动找到ChecklistManager如果没有手动设置
        if (checklistManager == null)
        {
            checklistManager = FindObjectOfType<ChecklistManager>();
        }
    }

    void Update()
    {
        // 检查时间缩放
        if (Time.timeScale == 0)
        {
            Debug.Log("游戏被暂停了，Time.timeScale = 0");
            return;
        }

        // 添加持续的心跳检测
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log($"T键按下，Time.timeScale = {Time.timeScale}");
        }

        // 在clipboard视图时，按ESC返回
        if (Input.GetKeyDown(KeyCode.Escape) && isViewingClipboard)
        {
            Debug.Log("检测到ESC键，返回原视角");
            ReturnToOriginalView();
        }
    }

    // 公共方法：由ChecklistManager调用以触发clipboard视图
    public void TriggerClipboardView()
    {
        if (!isViewingClipboard)
        {
            Debug.Log("ChecklistManager触发了clipboard视图切换");

            // 获取ScoreManager用于评估
            ScoreManager scoreManager = ScoreManager.Instance;
            if (scoreManager == null)
            {
                scoreManager = FindObjectOfType<ScoreManager>();
                Debug.Log("通过FindObjectOfType找到ScoreManager");
            }

            if (scoreManager == null)
            {
                Debug.LogError("找不到ScoreManager脚本！无法生成评估报告");
                // 即使找不到ScoreManager，也切换视角
                StartCoroutine(SwitchToClipboardView());
            }
            else
            {
                Debug.Log("找到ScoreManager，开始切换视角并生成报告");
                // 切换视角的同时启动评估
                StartCoroutine(SwitchToClipboardAndEvaluate(scoreManager));
            }
        }
        else
        {
            Debug.Log("当前已在clipboard视角");
        }
    }

    // 仅切换视角（无评估）
    private IEnumerator SwitchToClipboardView()
    {
        yield return StartCoroutine(SwitchToClipboard());
    }

    // 切换视角的同时启动评估
    private IEnumerator SwitchToClipboardAndEvaluate(ScoreManager scoreManager)
    {
        yield return StartCoroutine(SwitchToClipboard());

        // 视角切换完成后启动评估
        if (scoreManager != null)
        {
            Debug.Log("视角切换完成，开始评估");
            scoreManager.SubmitEvaluation();
        }
        else
        {
            Debug.LogError("ScoreManager为空，无法启动评估");
        }
    }

    // 核心的视角切换逻辑
    private IEnumerator SwitchToClipboard()
    {
        if (isViewingClipboard || clipboardViewPosition == null)
        {
            Debug.Log("无法切换：已在clipboard视角或clipboardViewPosition为空");
            yield break;
        }

        Debug.Log("开始切换到clipboard视角");

        isViewingClipboard = true;

        // 隐藏字幕UI（强制隐藏所有子对象）
        if (subtitleUI != null)
        {
            HideSubtitleCompletely(subtitleUI);
            Debug.Log("隐藏字幕UI及其所有子对象");
        }

        // 摄像机平滑移动到clipboard位置
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        Vector3 targetPos = clipboardViewPosition.position;
        Quaternion targetRot = clipboardViewPosition.rotation;

        Debug.Log($"摄像机从 {startPos} 移动到 {targetPos}");

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / transitionDuration;
            float curveValue = transitionCurve.Evaluate(progress);

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, targetRot, curveValue);

            yield return null;
        }

        // 确保最终位置准确
        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;

        // 激活clipboard区域
        if (clipboardReport != null)
        {
            clipboardReport.SetActive(true);
            Debug.Log("激活clipboard区域");
        }

        Debug.Log("摄像机切换完成");
    }

    public void ReturnToOriginalView()
    {
        if (!isViewingClipboard) return;

        Debug.Log("开始返回原始视角");
        StartCoroutine(TransitionToOriginal());
    }

    private IEnumerator TransitionToOriginal()
    {
        // 隐藏clipboard报告
        if (clipboardReport != null)
        {
            clipboardReport.SetActive(false);
            Debug.Log("隐藏clipboard报告");
        }

        // 重新显示字幕UI（恢复所有子对象）
        if (subtitleUI != null)
        {
            ShowSubtitleCompletely(subtitleUI);
            Debug.Log("重新显示字幕UI及其所有子对象");
        }

        // 摄像机平滑移动回原位置
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        Debug.Log($"摄像机从 {startPos} 返回到 {originalPosition}");

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / transitionDuration;
            float curveValue = transitionCurve.Evaluate(progress);

            mainCamera.transform.position = Vector3.Lerp(startPos, originalPosition, curveValue);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, originalRotation, curveValue);

            yield return null;
        }

        // 确保最终位置准确
        mainCamera.transform.position = originalPosition;
        mainCamera.transform.rotation = originalRotation;

        isViewingClipboard = false;
        Debug.Log("返回原始视角完成");

        // 重要：返回主视角时重置checklist
        ResetChecklist();
    }

    private void ResetChecklist()
    {
        if (checklistManager != null)
        {
            checklistManager.ShowCheckListPanel(); // 重新显示CheckList面板
            checklistManager.ResetAllTogglesPublic(); // 重置Toggle状态
            Debug.Log("已重新显示CheckListPanel并重置checklist状态");
        }
        else
        {
            Debug.LogWarning("ChecklistManager引用为空，无法重置checklist");
        }
    }

    // 检查当前是否在查看clipboard
    public bool IsViewingClipboard()
    {
        return isViewingClipboard;
    }

    // 强制隐藏字幕UI及其所有子对象
    private void HideSubtitleCompletely(GameObject subtitleObj)
    {
        if (subtitleObj == null) return;

        // 隐藏自身
        subtitleObj.SetActive(false);

        // 递归隐藏所有子对象
        foreach (Transform child in subtitleObj.transform)
        {
            child.gameObject.SetActive(false);
        }

        Debug.Log($"完全隐藏了 {subtitleObj.name} 及其 {subtitleObj.transform.childCount} 个子对象");
    }

    // 恢复显示字幕UI及其所有子对象
    private void ShowSubtitleCompletely(GameObject subtitleObj)
    {
        if (subtitleObj == null) return;

        // 显示自身
        subtitleObj.SetActive(true);

        // 递归显示所有子对象
        foreach (Transform child in subtitleObj.transform)
        {
            child.gameObject.SetActive(true);
        }

        Debug.Log($"完全显示了 {subtitleObj.name} 及其 {subtitleObj.transform.childCount} 个子对象");
    }
}