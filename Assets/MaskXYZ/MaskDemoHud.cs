using UnityEngine;
using UnityEngine.UI;

namespace MaskXYZ
{
    public sealed class MaskDemoHud : MonoBehaviour
    {
        [SerializeField] MaskAxisCamera controller;
        [SerializeField] Button maskButton;
        [SerializeField] Button[] axisButtons = new Button[6];
        [SerializeField] Text stateText;
        [SerializeField] Text viewText;
        Button[] primaryButtons;

        readonly Color active = new Color(0.53f, 0.25f, 0.95f);
        readonly Color inactive = new Color(0.17f, 0.18f, 0.22f);

        public void Configure(MaskAxisCamera cameraController, Button equipButton,
            Button[] modeButtons, Text status, Text view)
        {
            controller = cameraController;
            maskButton = equipButton;
            axisButtons = modeButtons;
            stateText = status;
            viewText = view;
        }

        void Awake()
        {
            var font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            foreach (var label in GetComponentsInChildren<Text>(true))
                label.font = font;

            maskButton.onClick.AddListener(controller.ToggleMask);
            // Existing package scenes have six buttons; rebuilt scenes have three.
            primaryButtons = axisButtons.Length >= 6
                ? new[] { axisButtons[0], axisButtons[2], axisButtons[4] }
                : axisButtons;
            string[] labels = { "1  X / -X", "2  Y / -Y", "3  Z / -Z" };
            for (int i = 0; i < primaryButtons.Length && i < 3; i++)
            {
                var button = primaryButtons[i];
                if (!button) continue;
                var rect = button.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(157 + 187 * i, -93);
                rect.sizeDelta = new Vector2(171, 55);
                var text = button.GetComponentInChildren<Text>();
                if (text)
                {
                    text.text = labels[i];
                    text.rectTransform.sizeDelta = new Vector2(171, 55);
                }
                switch (i)
                {
                    case 0: button.onClick.AddListener(controller.PressX); break;
                    case 1: button.onClick.AddListener(controller.PressY); break;
                    case 2: button.onClick.AddListener(controller.PressZ); break;
                }
            }
            if (axisButtons.Length >= 6)
                foreach (int i in new[] { 1, 3, 5 })
                    if (axisButtons[i]) axisButtons[i].gameObject.SetActive(false);
        }

        void Update()
        {
            if (!controller) return;
            bool equipped = controller.IsEquipped;
            stateText.text = equipped ? "МАСКА  •  ИЩИТЕ СВОБОДНЫЙ ПУТЬ" : "БЕЗ МАСКИ  •  ПУТЬ ЗАКРЫТ";
            int mode = (int)controller.CurrentView;
            string[] captions =
            {
                "ВИД СБОКУ ВДОЛЬ ОСИ X", "ВИД СБОКУ ВДОЛЬ ОСИ -X",
                "ВИД СВЕРХУ ВДОЛЬ ОСИ Y", "ВИД СНИЗУ ВДОЛЬ ОСИ -Y",
                "ВИД СПЕРЕДИ ВДОЛЬ ОСИ Z", "ВИД СЗАДИ ВДОЛЬ ОСИ -Z"
            };
            viewText.text = equipped ? captions[mode] : "M — НАДЕТЬ МАСКУ";
            maskButton.GetComponent<Image>().color = equipped ? active : inactive;
            for (int i = 0; i < primaryButtons.Length; i++)
                if (primaryButtons[i])
                {
                    primaryButtons[i].interactable = equipped;
                    primaryButtons[i].GetComponent<Image>().color = equipped && i == mode / 2
                        ? active : inactive;
                }
        }
    }
}
