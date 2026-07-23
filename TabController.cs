using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
#if IL2CPP
using Il2CppInterop.Runtime;
#endif

namespace BetterCounterOffer
{
    public class Tab
    {
        public string id;
        public Button button;
        public Image background;
        public Text buttonText;

        public void SetColor(Color bgColor, Color textColor)
        {
            SetBgColor(bgColor);
            SetTextColor(textColor);
        }

        public void SetBgColor(Color newColor)
        {
            if (this.background != null) this.background.color = newColor;
        }

        public void SetTextColor(Color newColor)
        {
            if (this.buttonText != null) this.buttonText.color = newColor;
        }
    }

    public class TabController
    {
        public Dictionary<string, Tab> allTabs = new Dictionary<string, Tab>();
        public Tab selectedTab = null;

        public float clickBuffer = 0.3f;
        public float prevTime = 0;

        public GameObject filterbuttons;
        public Transform parent;
        public Font font;

        public Color textActive = new Color(1f, 1f, 1f);
        public Color textDisabled = new Color(0.85f, 0.88f, 0.92f);
        public Color tabIdle = new Color(0.10f, 0.32f, 0.52f);
        public Color tabHover = new Color(0.15f, 0.42f, 0.65f);
        public Color tabActive = new Color(0.06f, 0.22f, 0.40f);

        public TabController(Transform parent)
        {
            this.parent = parent;
            InitFilterButtons();
        }

        private void InitFilterButtons()
        {
            filterbuttons = new GameObject("Filter_Buttons");
            if (this.parent != null)
            {
                filterbuttons.transform.SetParent(this.parent, false);
                filterbuttons.transform.SetAsLastSibling();
            }
            filterbuttons.AddComponent<CanvasRenderer>();
            RectTransform containerRectTrans = filterbuttons.AddComponent<RectTransform>();
            containerRectTrans.anchorMin = new Vector2(0.5f, 1f);
            containerRectTrans.anchorMax = new Vector2(0.5f, 1f);
            containerRectTrans.pivot = new Vector2(0.5f, 1f);
            containerRectTrans.anchoredPosition = new Vector2(0f, 32f);
            containerRectTrans.sizeDelta = new Vector2(240, 28);

            GridLayoutGroup containerGrid = filterbuttons.AddComponent<GridLayoutGroup>();
            containerGrid.cellSize = new Vector2(72, 26);
            containerGrid.spacing = new Vector2(6, 0);
            containerGrid.childAlignment = TextAnchor.MiddleCenter;
            containerGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            containerGrid.constraintCount = 3;
        }

        public void AddTab(string id, string text)
        {
            if (allTabs.ContainsKey(id)) return;

            if (filterbuttons == null) return;
            Tab newTab = CreateNewTab(filterbuttons.transform, id, text);
            allTabs.Add(id, newTab);
        }

        public void SetSelected(string key)
        {
            if (!allTabs.ContainsKey(key)) return;
            ResetTabs();
            Tab selected = allTabs[key];
            selectedTab = selected;
            selected.SetColor(tabActive, textActive);
        }

        public Tab CreateNewTab(Transform parent, string title, string text)
        {
            GameObject buttonGo = new GameObject($"{title}_Button");
            buttonGo.transform.SetParent(parent, false);
            buttonGo.AddComponent<CanvasRenderer>();
            Button button = buttonGo.AddComponent<Button>();
            Image buttonImg = buttonGo.AddComponent<Image>();
            buttonImg.color = tabIdle;

            GameObject buttonTextGo = new GameObject($"{title}_Button_Text");
            buttonTextGo.transform.SetParent(buttonGo.transform, false);
            Text buttonText = buttonTextGo.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.fontSize = 13;
            buttonText.color = textDisabled;
            buttonText.alignment = TextAnchor.MiddleCenter;

            var textRect = buttonTextGo.GetComponent<RectTransform>();
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.anchoredPosition = new Vector2(0, 0);

            Tab newTab = new Tab
            {
                id = title,
                button = button,
                background = buttonImg,
                buttonText = buttonText
            };

            EventTrigger.Entry eventEntryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
#if IL2CPP
            eventEntryEnter.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>((BaseEventData eventData) => HandleButtonEnter(newTab)));
#elif MONO
            eventEntryEnter.callback.AddListener((BaseEventData eventData) => HandleButtonEnter(newTab));
#endif

            EventTrigger.Entry eventEntryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
#if IL2CPP
            eventEntryExit.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>((BaseEventData eventData) => HandleButtonExit(newTab)));
#elif MONO
            eventEntryExit.callback.AddListener((BaseEventData eventData) => HandleButtonExit(newTab));
#endif

            EventTrigger.Entry eventEntryClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
#if IL2CPP
            eventEntryClick.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>((BaseEventData eventData) => HandleButtonClick(newTab)));
#elif MONO
            eventEntryClick.callback.AddListener((BaseEventData eventData) => HandleButtonClick(newTab));
#endif

            EventTrigger events = buttonGo.AddComponent<EventTrigger>();
            events.triggers.Add(eventEntryEnter);
            events.triggers.Add(eventEntryExit);
            events.triggers.Add(eventEntryClick);

            return newTab;
        }

        public void HandleButtonEnter(Tab currTab)
        {
            if (selectedTab != null && selectedTab == currTab) return;
            currTab.SetBgColor(tabHover);
        }

        public void HandleButtonExit(Tab currTab)
        {
            ResetTabs();
        }

        public void HandleButtonClick(Tab currTab)
        {
            float currTime = Time.time;
            if (currTime - prevTime > clickBuffer)
            {
                CounterOfferUI.TabSelected(currTab);
                selectedTab = currTab;
                ResetTabs();
                currTab.SetColor(tabActive, textActive);
                prevTime = currTime;
            }
        }

        public void ResetTabs()
        {
            foreach (var kvp in allTabs)
            {
                if (selectedTab != null && kvp.Value == selectedTab)
                {
                    kvp.Value.SetColor(tabActive, textActive);
                }
                else
                {
                    kvp.Value.SetColor(tabIdle, textDisabled);
                }
            }
        }
    }
}
