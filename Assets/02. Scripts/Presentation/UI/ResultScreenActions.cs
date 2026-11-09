using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ResultScreenActions : MonoBehaviour
{
    private void Awake()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;
            string text = label.text.Replace(" ", "").Replace("\n", "").Trim();
            if (text == "스토리캠페인")
            {
                bool campaignConnected = false;
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    campaignConnected |= button.onClick.GetPersistentTarget(i) is PauseMenuController &&
                        button.onClick.GetPersistentMethodName(i) == "OpenCampaignFromResult";
                if (!campaignConnected) button.onClick.AddListener(OpenCampaign);
                continue;
            }
            if (text != "다시시도") continue;
            bool connected = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                connected |= button.onClick.GetPersistentTarget(i) != null &&
                    !string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i));
            if (!connected) button.onClick.AddListener(Retry);
        }
    }

    public void OpenCampaign()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            var controller = root.GetComponentInChildren<PauseMenuController>(true);
            if (controller == null) continue;
            controller.OpenCampaignFromResult();
            return;
        }
    }

    public void Retry()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            var controller = root.GetComponentInChildren<PauseMenuController>(true);
            if (controller == null) continue;
            controller.RequestResultRetry();
            return;
        }
        Debug.LogError("재시도 확인 팝업을 표시할 PauseMenuController를 연결하세요.", this);
    }

}
