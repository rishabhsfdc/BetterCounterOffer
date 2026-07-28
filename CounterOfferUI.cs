using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
#if IL2CPP
using GenericCol = Il2CppSystem.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.UI.Handover;
using Il2CppTMPro;
#elif MONO
using GenericCol = System.Collections.Generic;
using ScheduleOne;
using ScheduleOne.UI.Phone;
using ScheduleOne.Product;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.UI.Handover;
using TMPro;
#endif

namespace BetterCounterOffer
{
    public static class CounterOfferUI
    {
        public static GameObject PlayerRef = null;
        public static GameObject popupRef = null;
        public static GameObject productSelectorRef = null;

        public static GameObject initialOfferGO = null;
        public static GameObject maxCashGO = null;
        public static GameObject successRateGO = null;

        public static Text initialOfferText = null;
        public static Text successRateText = null;
        public static Text maxCashText = null;
        public static Text fairPriceText = null;
        public static Text btnText = null;
        public static Image btnBg = null;
        public static Font gameFont = null;
        public static TabController selectorTabControl;

        public static bool displayAll = false;
        public static string currTab = "Favorites";
        public static float prevTime = 0;
        public static Gradient colorMap = new Gradient();
        public static GenericCol.List<ProductDefinition> currList = null;
        public static CounterOfferProductSelector selectorInterface = null;
        public static CounterofferInterface offerInterface = null;

        public static bool isUpdatingPrice = false;
        public static int labelCount = 0;
        public static float capturedInitialPrice = 0f;

        public static void SetPriceSafely(CounterofferInterface instance, float newPrice)
        {
            if (isUpdatingPrice || instance == null || instance.PriceSelector == null) return;
            try
            {
                isUpdatingPrice = true;
                float minVal = instance.PriceSelector.MinValue;
                float maxVal = instance.PriceSelector.MaxValue > 0 ? instance.PriceSelector.MaxValue : 9999f;
                float clampedPrice = Mathf.Clamp(Mathf.Floor(newPrice), minVal, maxVal);
                instance.PriceSelector.SetAmount(clampedPrice);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting price safely: {ex.Message}");
            }
            finally
            {
                isUpdatingPrice = false;
            }
        }

        public static void EnsureUIInitialized(CounterofferInterface instance)
        {
            if (instance == null) return;

            if (colorMap == null || colorMap.colorKeys == null || colorMap.colorKeys.Length == 0)
            {
                GameObject handOverScreen = GameObject.Find("UI/HandoverScreen");
                if (handOverScreen != null)
                {
                    HandoverScreen hands = handOverScreen.GetComponent<HandoverScreen>();
                    if (hands != null && hands.SuccessColorMap != null)
                    {
                        colorMap = hands.SuccessColorMap;
                    }
                }
            }

            Transform mainCardContainer = null;
            if (instance.TitleLabel != null && instance.TitleLabel.transform.parent != null && instance.TitleLabel.transform.parent.parent != null)
            {
                mainCardContainer = instance.TitleLabel.transform.parent.parent;
            }
            if (mainCardContainer == null && instance.FairPriceLabel != null && instance.FairPriceLabel.transform.parent != null && instance.FairPriceLabel.transform.parent.parent != null)
            {
                mainCardContainer = instance.FairPriceLabel.transform.parent.parent;
            }
            if (mainCardContainer == null && instance.Container != null)
            {
                mainCardContainer = instance.Container.transform;
            }
            if (mainCardContainer == null) mainCardContainer = instance.transform;

            CleanupExistingLabels(mainCardContainer);
            CleanupExistingLabels(instance.transform);
            if (instance.TitleLabel != null) CleanupExistingLabels(instance.TitleLabel.transform.parent);
            if (instance.FairPriceLabel != null) CleanupExistingLabels(instance.FairPriceLabel.transform.parent);

            CreateLabelsCloned(instance, mainCardContainer);
            UpdateSelectorUI(instance);
        }

        private static void CleanupExistingLabels(Transform parent)
        {
            if (parent == null) return;
            string[] targetNames = new string[] { "OfferInformation", "ModInitialCash", "ModMaxCash", "ModSuccessRate" };
            foreach (string name in targetNames)
            {
                Transform found = parent.Find(name);
                if (found != null)
                {
                    try { UnityEngine.Object.DestroyImmediate(found.gameObject); } catch { }
                }
            }
            initialOfferGO = null;
            maxCashGO = null;
            successRateGO = null;
            initialOfferText = null;
            maxCashText = null;
            successRateText = null;
        }

        public static void OnPopupOpen(CounterofferInterface instance)
        {
            if (instance == null) return;
            offerInterface = instance;
            EnsureUIInitialized(instance);

            Customer currCustomer = (instance.conversation != null && instance.conversation.sender != null) 
                ? instance.conversation.sender.GetComponent<Customer>() 
                : null;

            // 1. Capture customer's TRUE initial offer price BEFORE auto-filling!
            float initialPrice = 0f;
            int baseQuantity = 1;
            if (currCustomer != null && currCustomer.OfferedContractInfo != null)
            {
                initialPrice = currCustomer.OfferedContractInfo.Payment;
                if (currCustomer.OfferedContractInfo.Products != null && currCustomer.OfferedContractInfo.Products.entries.Count > 0)
                {
                    baseQuantity = Math.Max(1, currCustomer.OfferedContractInfo.Products.entries[0].Quantity);
                }
            }
            if (initialPrice <= 0f && instance.PriceSelector != null)
            {
                initialPrice = instance.PriceSelector.SelectedAmount;
            }
            if (baseQuantity <= 0) baseQuantity = instance.quantity > 0 ? instance.quantity : 1;
            capturedInitialPrice = initialPrice;

            // 2. Calculate optimal (quantity, price) combo for Max 100% Success Payout
            if (currCustomer != null && instance.PriceSelector != null && instance.selectedProduct != null)
            {
                float maxSpend = CalculateSpendingLimits(currCustomer);
                if (maxSpend > 0)
                {
                    int bestQuantity;
                    float bestPrice;
                    FindOptimal100PercentDeal(currCustomer, instance.selectedProduct, baseQuantity, maxSpend, out bestQuantity, out bestPrice);

                    if (bestPrice > 0f)
                    {
                        SetQuantitySafely(instance, bestQuantity);
                        SetPriceSafely(instance, bestPrice);
                    }
                    else
                    {
                        int safeMaxSpend = Mathf.FloorToInt(maxSpend - 0.0001f);
                        SetPriceSafely(instance, safeMaxSpend);
                    }
                }
            }

            UpdateAllLabels(instance);
        }

        public static void UpdateAllLabels(CounterofferInterface instance)
        {
            if (instance == null) return;

            Customer currCustomer = (instance.conversation != null && instance.conversation.sender != null) 
                ? instance.conversation.sender.GetComponent<Customer>() 
                : null;

            float initialPrice = capturedInitialPrice;
            if (initialPrice <= 0f && currCustomer != null && currCustomer.OfferedContractInfo != null)
            {
                initialPrice = currCustomer.OfferedContractInfo.Payment;
            }
            if (initialPrice <= 0f && instance.PriceSelector != null)
            {
                initialPrice = instance.PriceSelector.SelectedAmount;
            }

            float maxSpend = (currCustomer != null) ? CalculateSpendingLimits(currCustomer) : 1000f;
            float currentPrice = (instance.PriceSelector != null) ? instance.PriceSelector.SelectedAmount : initialPrice;

            if (!CounterOfferConfig.disableAllLabels)
            {
                if (!CounterOfferConfig.disableInitialOffer)
                {
                    if (CounterOfferConfig.enablePricePerUnit && instance.quantity > 0)
                    {
                        SetInitialPriceText(initialPrice / instance.quantity, true);
                        SetFairPriceText(currentPrice / instance.quantity);
                    }
                    else
                    {
                        SetInitialPriceText(initialPrice);
                    }
                }

                if (!CounterOfferConfig.disableMaxLimit)
                {
                    SetMaxCashText(maxSpend);
                }

                if (!CounterOfferConfig.disableSuccessRate)
                {
                    float successChance = (currCustomer != null) 
                        ? CalculateSuccessProbability(currCustomer, instance.selectedProduct, instance.quantity, currentPrice)
                        : 1.0f;
                    SetSuccessRateText(successChance);
                }
            }
        }

        public static void SetLabelText(GameObject go, string textStr, Color color, bool isBold = false)
        {
            if (go == null) return;

#if IL2CPP
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.enableAutoSizing = false;
                tmp.fontSize = 16f;
                tmp.fontSizeMin = 16f;
                tmp.fontSizeMax = 16f;
                tmp.text = textStr;
                tmp.color = color;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = isBold ? FontStyles.Bold : FontStyles.Normal;
                return;
            }
#elif MONO
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.enableAutoSizing = false;
                tmp.fontSize = 16f;
                tmp.fontSizeMin = 16f;
                tmp.fontSizeMax = 16f;
                tmp.text = textStr;
                tmp.color = color;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = isBold ? FontStyles.Bold : FontStyles.Normal;
                return;
            }
#endif

            var txt = go.GetComponent<Text>();
            if (txt == null) txt = go.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = textStr;
                txt.color = color;
                txt.fontSize = 16;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.fontStyle = isBold ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        public static void SetSuccessRateText(float success)
        {
            if (successRateGO == null && successRateText == null) return;
            int pct = Mathf.RoundToInt(success * 100);
            string text = $"{pct}% Chance of Success";
            
            Color textCol = new Color(0f, 0.75f, 0.15f, 1f); // Vibrant Green
            if (pct < 40) textCol = new Color(0.85f, 0.15f, 0.15f, 1f); // Red
            else if (pct < 75) textCol = new Color(0.85f, 0.5f, 0f, 1f); // Orange

            if (successRateGO != null)
            {
                SetLabelText(successRateGO, text, textCol, true);
            }
            else if (successRateText != null)
            {
                successRateText.text = text;
                successRateText.color = textCol;
            }
        }

        public static void SetMaxCashText(float maxSpend)
        {
            Color color = new Color(0.35f, 0.35f, 0.35f, 1f);
            int safeMax = Mathf.FloorToInt(maxSpend - 0.0001f);
            string text = $"Spend Limit: ${safeMax}";

            if (maxCashGO != null)
            {
                SetLabelText(maxCashGO, text, color, false);
            }
            else if (maxCashText != null)
            {
                maxCashText.text = text;
                maxCashText.color = color;
            }
        }

        public static void SetInitialPriceText(float initialPrice, bool ppu = false)
        {
            Color color = new Color(0.35f, 0.35f, 0.35f, 1f);
            string text = ppu ? $"Initial Offer ${Mathf.RoundToInt(initialPrice)} per Unit" : $"Initial Offer ${Mathf.RoundToInt(initialPrice)}";

            if (initialOfferGO != null)
            {
                SetLabelText(initialOfferGO, text, color, false);
            }
            else if (initialOfferText != null)
            {
                initialOfferText.text = text;
                initialOfferText.color = color;
            }
        }

        public static void SetFairPriceText(float fairPrice)
        {
            if (fairPriceText == null) return;
            fairPriceText.text = $"Price: ${Mathf.RoundToInt(fairPrice)} per Unit";
        }

        public static float CalculateSpendingLimits(Customer customer)
        {
            if (customer == null || customer.CustomerData == null) return 1000f;
            CustomerData customerData = customer.CustomerData;
            float relationDelta = (customer.NPC != null && customer.NPC.RelationData != null) ? customer.NPC.RelationData.RelationDelta : 0f;
            float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(relationDelta / 5f);
            var orderDays = new GenericCol.List<EDay>();
            customerData.GetOrderDays(customer.CurrentAddiction, relationDelta / 5f, orderDays);
            int count = orderDays.Count > 0 ? orderDays.Count : 1;
            float maxSpend = (adjustedWeeklySpend / count) * 3f;
            return maxSpend;
        }

        public static float CalculateSuccessProbability(Customer customer, ProductDefinition product, int quantity, float price)
        {
            if (customer == null || customer.CustomerData == null || product == null || quantity <= 0) return 0.5f;

            float relationDelta = (customer.NPC != null && customer.NPC.RelationData != null) ? customer.NPC.RelationData.RelationDelta : 0f;
            float adjustedWeeklySpend = customer.CustomerData.GetAdjustedWeeklySpend(relationDelta / 5f);
            var orderDays = new GenericCol.List<EDay>();
            customer.CustomerData.GetOrderDays(customer.CurrentAddiction, relationDelta / 5f, orderDays);
            int count = orderDays.Count > 0 ? orderDays.Count : 1;
            float num = adjustedWeeklySpend / count;

            if (price >= num * 3f) return 0f;

            float basePayment = 0f;
            int baseQuantity = 1;
            if (customer.OfferedContractInfo != null && customer.OfferedContractInfo.Products != null && customer.OfferedContractInfo.Products.entries.Count > 0)
            {
                baseQuantity = Math.Max(1, customer.OfferedContractInfo.Products.entries[0].Quantity);
                basePayment = customer.OfferedContractInfo.Payment;
            }

            float valueProposition = Customer.GetValueProposition(product, basePayment / baseQuantity);
            float productEnjoyment = customer.GetProductEnjoyment(product, customer.CustomerData.Standards.GetCorrespondingQuality());
            float num2 = Mathf.InverseLerp(-1f, 1f, productEnjoyment);
            float valueProposition2 = Customer.GetValueProposition(product, price / quantity);
            float num3 = Mathf.Pow(quantity / (float)baseQuantity, 0.6f);
            float num4 = Mathf.Lerp(0f, 2f, num3 * 0.5f);
            float num5 = Mathf.Lerp(1f, 0f, Mathf.Abs(num4 - 1f));

            if (valueProposition2 * num5 > valueProposition) return 1f;
            if (valueProposition2 < 0.12f) return 0f;

            float num6 = productEnjoyment * valueProposition;
            float num7 = num2 * num5 * valueProposition2;

            if (num7 > num6) return 1f;

            float num8 = num6 - num7;
            float num9 = Mathf.Lerp(0f, 1f, num8 / 0.2f);
            float normRelation = (customer.NPC != null && customer.NPC.RelationData != null) ? customer.NPC.RelationData.NormalizedRelationDelta : 0f;
            float t = Mathf.Max(customer.CurrentAddiction, normRelation);
            float num10 = Mathf.Lerp(0f, 0.2f, t);

            if (num9 <= num10) return 1f;
            if (num9 - num10 >= 0.9f) return 0f;

            float probability = (0.9f + num10 - num9) / 0.9f;
            return Mathf.Clamp(probability, 0f, 1f);
        }

        public static void SetQuantitySafely(CounterofferInterface instance, int targetQuantity)
        {
            if (instance == null || targetQuantity <= 0) return;
            try
            {
                float change = targetQuantity - instance.quantity;
                if (Mathf.Abs(change) > 0.001f)
                {
                    instance.ChangeQuantity(change);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting quantity safely: {ex.Message}");
            }
        }

        public static float FindMax100PercentPriceForQuantity(Customer customer, ProductDefinition product, int quantity, float maxSpend)
        {
            if (customer == null || product == null || quantity <= 0) return 0f;
            int low = 1;
            int high = Mathf.FloorToInt(maxSpend - 0.0001f);
            int best100Price = 0;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                float prob = CalculateSuccessProbability(customer, product, quantity, mid);
                if (prob >= 0.999f)
                {
                    best100Price = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best100Price;
        }

        public static void FindOptimal100PercentDeal(Customer customer, ProductDefinition product, int baseQuantity, float maxSpend, out int bestQuantity, out float bestPrice)
        {
            bestQuantity = baseQuantity > 0 ? baseQuantity : 1;
            bestPrice = 0f;
            float maxRevenue = -1f;

            int minQ = 1;
            int maxQ = Math.Max(10, baseQuantity + 5);

            for (int q = minQ; q <= maxQ; q++)
            {
                float p = FindMax100PercentPriceForQuantity(customer, product, q, maxSpend);
                if (p > maxRevenue && p > 0f)
                {
                    maxRevenue = p;
                    bestQuantity = q;
                    bestPrice = p;
                }
            }

            if (bestPrice <= 0f)
            {
                bestQuantity = baseQuantity > 0 ? baseQuantity : 1;
                bestPrice = FindMax100PercentPriceForQuantity(customer, product, bestQuantity, maxSpend);
            }
        }

        public static void AutoSet100PercentPriceForCurrentState(CounterofferInterface instance)
        {
            if (instance == null || isUpdatingPrice) return;
            Customer currCustomer = (instance.conversation != null && instance.conversation.sender != null) 
                ? instance.conversation.sender.GetComponent<Customer>() 
                : null;

            if (currCustomer != null && instance.selectedProduct != null && instance.quantity > 0)
            {
                float maxSpend = CalculateSpendingLimits(currCustomer);
                if (maxSpend > 0)
                {
                    float max100Price = FindMax100PercentPriceForQuantity(currCustomer, instance.selectedProduct, instance.quantity, maxSpend);
                    if (max100Price > 0f)
                    {
                        SetPriceSafely(instance, max100Price);
                    }
                }
            }
        }

        public static void UpdateSuccessRate(CounterofferInterface instance)
        {
            UpdateAllLabels(instance);
        }

        public static void InitOnWake()
        {
        }

        private static void UpdateSelectorUI(CounterofferInterface instance)
        {
            if (instance == null || instance.ProductSelector == null) return;

            Transform selectorTrans = instance.ProductSelector.transform;
            selectorInterface = instance.ProductSelector;

            if (selectorTabControl != null && selectorTabControl.filterbuttons != null)
            {
                try { UnityEngine.Object.Destroy(selectorTabControl.filterbuttons); } catch { }
                selectorTabControl = null;
            }

            selectorTabControl = new TabController(selectorTrans);
            if (gameFont != null)
            {
                selectorTabControl.font = gameFont;
            }
            selectorTabControl.AddTab("Favorites", "Fave");
            selectorTabControl.AddTab("Listed", "Listed");
            selectorTabControl.AddTab("Discovered", "All");
            selectorTabControl.SetSelected(currTab);
        }

        public static void TabSelected(Tab selected)
        {
            currTab = selected.id;
            if (selectorInterface != null)
            {
                selectorInterface.RebuildResultsList();
            }
        }

        private static void SetupFullWidthLayout(GameObject go)
        {
            if (go == null) return;
            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(0f, 0f);
                rect.offsetMax = new Vector2(0f, 0f);
            }
            var layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 24f;
            layout.preferredHeight = 24f;
            layout.flexibleWidth = 1f;
        }

        private static void CreateLabelsCloned(CounterofferInterface instance, Transform cardContainer)
        {
            if (instance == null || cardContainer == null) return;

            GameObject templateGO = null;
            Transform subtitleTrans = cardContainer.Find("Customer/Subtitle");
            if (subtitleTrans != null)
            {
                templateGO = subtitleTrans.gameObject;
            }
            if (templateGO == null && instance.TitleLabel != null) templateGO = instance.TitleLabel.gameObject;
            if (templateGO == null && instance.FairPriceLabel != null) templateGO = instance.FairPriceLabel.gameObject;
            if (templateGO == null) return;

            Transform headerTrans = cardContainer.Find("Header");
            int targetIndex = (headerTrans != null) ? headerTrans.GetSiblingIndex() + 1 : 3;

            Color subLabelColor = new Color(0.35f, 0.35f, 0.35f, 1f);

            if (!CounterOfferConfig.disableInitialOffer)
            {
                initialOfferGO = UnityEngine.Object.Instantiate(templateGO, cardContainer);
                initialOfferGO.name = "ModInitialCash";
                initialOfferGO.transform.SetSiblingIndex(targetIndex++);
                SetupFullWidthLayout(initialOfferGO);
                SetLabelText(initialOfferGO, "Initial Offer $0", subLabelColor, false);
            }

            if (!CounterOfferConfig.disableMaxLimit)
            {
                maxCashGO = UnityEngine.Object.Instantiate(templateGO, cardContainer);
                maxCashGO.name = "ModMaxCash";
                maxCashGO.transform.SetSiblingIndex(targetIndex++);
                SetupFullWidthLayout(maxCashGO);
                SetLabelText(maxCashGO, "Spend Limit: $0", subLabelColor, false);
            }

            if (!CounterOfferConfig.disableSuccessRate)
            {
                successRateGO = UnityEngine.Object.Instantiate(templateGO, cardContainer);
                successRateGO.name = "ModSuccessRate";
                successRateGO.transform.SetSiblingIndex(targetIndex++);
                SetupFullWidthLayout(successRateGO);
                SetLabelText(successRateGO, "100% Chance of Success", new Color(0f, 0.75f, 0.15f, 1f), true);
            }
        }
    }
}
