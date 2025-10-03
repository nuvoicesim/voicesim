using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class ChecklistManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform checklistParent; // 包含Toggle的父对象
    public Button finishButton; // FinishButton
    public CameraClipboardController cameraController; // 引用CameraClipboardController
    public GameObject checkListPanel; // CheckListPanel整个面板

    [Header("Icon Mode Settings")]
    public Button checklistIconButton; // Checklist图标按钮
    public Button closeButton; // 右上角关闭按钮("X")
    public bool startAsIcon = true; // 是否以图标形式开始

    // 内部使用的checklist项目列表
    private List<ChecklistItem> checklistItems = new List<ChecklistItem>();
    private bool isPanelVisible = false; // 面板当前是否可见

    [System.Serializable]
    public class ChecklistItem
    {
        public string itemText;
        public bool isCompleted;
        public Toggle toggle;
    }

    private void Start()
    {
        // 自动找到CameraClipboardController如果没有手动设置
        if (cameraController == null)
        {
            cameraController = CameraClipboardController.Instance;
        }

        // 自动找到CheckListPanel如果没有手动设置
        if (checkListPanel == null)
        {
            checkListPanel = FindCheckListPanel();
        }

        // 自动找到Icon按钮如果没有手动设置
        if (checklistIconButton == null)
        {
            checklistIconButton = FindChecklistIconButton();
        }

        // 自动找到关闭按钮如果没有手动设置
        if (closeButton == null)
        {
            closeButton = FindCloseButton();
        }

        // 设置初始状态
        if (startAsIcon)
        {
            ShowIconOnly();
        }
        else
        {
            ShowPanelOnly();
        }

        // 绑定按钮事件
        SetupButtonEvents();

        // 设置现有的手动创建的UI
        SetupExistingUI();

        // 更新Finish按钮状态
        UpdateFinishButton();
    }

    // 设置按钮事件
    private void SetupButtonEvents()
    {
        // 绑定图标按钮事件 - 点击展开面板
        if (checklistIconButton != null)
        {
            checklistIconButton.onClick.AddListener(ShowCheckListPanel);
        }
        else
        {
            Debug.LogWarning("ChecklistIconButton未分配！");
        }

        // 绑定关闭按钮事件 - 点击收起为图标
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideCheckListPanel);
        }
        else
        {
            Debug.LogWarning("CloseButton未分配！");
        }

        // 绑定Finish按钮事件
        if (finishButton != null)
        {
            finishButton.onClick.AddListener(OnFinishClicked);
        }
        else
        {
            Debug.LogError("Finish Button未分配！请在Inspector中分配finishButton");
        }
    }

    // 自动查找CheckListPanel
    private GameObject FindCheckListPanel()
    {
        // 方法1: 通过名字查找
        GameObject panel = GameObject.Find("CheckListPanel");
        if (panel != null)
        {
            Debug.Log("自动找到CheckListPanel");
            return panel;
        }

        // 方法2: 从checklistParent向上查找包含"Panel"的父对象
        Transform current = checklistParent;
        while (current != null)
        {
            if (current.name.ToLower().Contains("panel"))
            {
                Debug.Log($"自动找到Panel: {current.name}");
                return current.gameObject;
            }
            current = current.parent;
        }

        Debug.LogWarning("未找到CheckListPanel，请手动分配");
        return null;
    }

    // 自动查找Checklist图标按钮
    private Button FindChecklistIconButton()
    {
        GameObject iconButton = GameObject.Find("ChecklistIcon");
        if (iconButton != null)
        {
            Button btn = iconButton.GetComponent<Button>();
            if (btn != null)
            {
                Debug.Log("自动找到ChecklistIcon按钮");
                return btn;
            }
        }
        Debug.LogWarning("未找到ChecklistIcon按钮，请手动分配");
        return null;
    }

    // 自动查找关闭按钮
    private Button FindCloseButton()
    {
        // 在CheckListPanel中查找名为"Close"或"X"的按钮
        if (checkListPanel != null)
        {
            Button[] buttons = checkListPanel.GetComponentsInChildren<Button>();
            foreach (Button btn in buttons)
            {
                if (btn.name.ToLower().Contains("close") || btn.name.ToLower().Contains("x"))
                {
                    Debug.Log($"自动找到关闭按钮: {btn.name}");
                    return btn;
                }
            }
        }
        Debug.LogWarning("未找到关闭按钮，请手动分配");
        return null;
    }

    // 设置现有UI的方法
    private void SetupExistingUI()
    {
        // 找到所有现有的Toggle组件
        Toggle[] existingToggles = checklistParent.GetComponentsInChildren<Toggle>();
        Debug.Log($"找到 {existingToggles.Length} 个现有Toggle");

        // 清空现有列表
        checklistItems.Clear();

        // 根据现有Toggle创建checklist项目
        foreach (Toggle toggle in existingToggles)
        {
            Text toggleText = toggle.GetComponentInChildren<Text>();
            string itemText = toggleText != null ? toggleText.text : $"Item {checklistItems.Count + 1}";

            ChecklistItem item = new ChecklistItem
            {
                itemText = itemText,
                isCompleted = false, // 强制设置为未完成状态
                toggle = toggle
            };

            checklistItems.Add(item);

            // 确保Toggle初始状态为未勾选
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = false; // 强制设置为未勾选

            // 绑定Toggle事件
            var currentItem = item;
            toggle.onValueChanged.AddListener((bool value) =>
            {
                OnToggleChanged(currentItem, value);
            });

            Debug.Log($"绑定Toggle: {itemText} - 初始状态: 未勾选");
        }

        Debug.Log($"成功设置 {checklistItems.Count} 个checklist项目，全部初始化为未勾选状态");
    }


    private void OnToggleChanged(ChecklistItem item, bool isChecked)
    {
        item.isCompleted = isChecked;
        UpdateFinishButton();
        Debug.Log($"Item '{item.itemText}' is now: {(isChecked ? "Completed" : "Incomplete")}");
    }

    private void UpdateFinishButton()
    {
        if (finishButton != null)
        {
            // 检查是否所有项目都完成
            bool allCompleted = checklistItems.Count > 0 && checklistItems.All(item => item.isCompleted);
            finishButton.interactable = allCompleted;

            // 改变按钮颜色
            ColorBlock colors = finishButton.colors;
            if (allCompleted)
            {
                colors.normalColor = Color.green;
                colors.highlightedColor = new Color(0f, 0.8f, 0f);
            }
            else
            {
                colors.normalColor = Color.gray;
                colors.highlightedColor = Color.gray;
            }
            finishButton.colors = colors;

            Debug.Log($"Finish button state: {(allCompleted ? "Enabled" : "Disabled")} - {checklistItems.Count(item => item.isCompleted)}/{checklistItems.Count}");
        }
    }

    private void OnFinishClicked()
    {
        Debug.Log("All checklist items completed! Triggering clipboard view...");

        // 隐藏CheckList面板，显示图标
        HideCheckListPanel();

        // 调用CameraClipboardController的切换方法
        if (cameraController != null)
        {
            cameraController.TriggerClipboardView();
        }
        else
        {
            Debug.LogError("CameraClipboardController reference is missing!");
        }

        // 完成后重置所有toggle
        ResetAllToggles();
    }

    // 显示图标，隐藏面板
    private void ShowIconOnly()
    {
        if (checklistIconButton != null)
        {
            checklistIconButton.gameObject.SetActive(true);
        }

        if (checkListPanel != null)
        {
            checkListPanel.SetActive(false);
        }

        isPanelVisible = false;
        Debug.Log("显示图标模式");
    }

    // 显示面板，隐藏图标
    private void ShowPanelOnly()
    {
        if (checklistIconButton != null)
        {
            checklistIconButton.gameObject.SetActive(false);
        }

        if (checkListPanel != null)
        {
            checkListPanel.SetActive(true);
        }

        isPanelVisible = true;
        Debug.Log("显示面板模式");
    }

    // 显示CheckList面板（从图标展开）
    public void ShowCheckListPanel()
    {
        ShowPanelOnly();
        Debug.Log("从图标展开CheckListPanel");
    }

    // 隐藏CheckList面板（收起为图标）
    public void HideCheckListPanel()
    {
        ShowIconOnly();
        Debug.Log("收起CheckListPanel为图标");
    }

    private void ResetAllToggles()
    {
        Debug.Log("Resetting all toggles...");

        foreach (var item in checklistItems)
        {
            item.isCompleted = false;
            if (item.toggle != null)
            {
                // 临时移除监听器避免触发事件
                item.toggle.onValueChanged.RemoveAllListeners();
                item.toggle.isOn = false;

                // 重新添加监听器
                var currentItem = item;
                item.toggle.onValueChanged.AddListener((bool value) =>
                {
                    OnToggleChanged(currentItem, value);
                });
            }
        }

        UpdateFinishButton();
    }

    // 公共方法：手动重置（供外部调用）
    [ContextMenu("Reset All Toggles")]
    public void ResetAllTogglesPublic()
    {
        ResetAllToggles();
    }


    // 调试方法
    [ContextMenu("Debug Checklist Status")]
    public void DebugChecklistStatus()
    {
        Debug.Log($"=== Checklist Status ({checklistItems.Count} items) ===");
        for (int i = 0; i < checklistItems.Count; i++)
        {
            var item = checklistItems[i];
            Debug.Log($"{i + 1}. {item.itemText} - {(item.isCompleted ? "✓" : "✗")}");
        }

        int completedCount = checklistItems.Count(item => item.isCompleted);
        Debug.Log($"Completed: {completedCount}/{checklistItems.Count}");
        Debug.Log($"Finish Button Enabled: {(finishButton != null ? finishButton.interactable.ToString() : "NULL")}");
        Debug.Log($"Panel Visible: {isPanelVisible}");
    }

    // 测试方法：切换显示模式
    [ContextMenu("Toggle Display Mode")]
    public void ToggleDisplayMode()
    {
        if (isPanelVisible)
        {
            HideCheckListPanel();
        }
        else
        {
            ShowCheckListPanel();
        }
    }

    // 获取当前显示状态
    public bool IsPanelVisible()
    {
        return isPanelVisible;
    }
}