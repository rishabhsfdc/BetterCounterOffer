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
#elif MONO
using GenericCol = System.Collections.Generic;
using ScheduleOne;
using ScheduleOne.UI.Phone;
using ScheduleOne.Product;
using ScheduleOne.Economy;
using ScheduleOne.GameTime;
using ScheduleOne.UI.Handover;
#endif

namespace BetterCounterOffer
{
    public static class CounterOfferUI
    {
        public static GameObject PlayerRef = null;
        public static GameObject popupRef = null;
        public static GameObject productSelectorRef = null;

        public static GameObject offerInfoGO = null;
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

        public static void SetPriceSafely(CounterofferInterface instance, float newPrice)
        {
            if (isUpdatingPrice || instance == null || instance.PriceSelector == null) return;
            try
            {
                isUpdatingPrice = true;
                float minVal = instance.PriceSelector.MinValue;
                float maxVal = instance.PriceSelector.MaxValue > 0 ? instance.PriceSelector.MaxValue : 9999f;
                float clampedPrice = Mathf.Clamp(Mathf.Floor(newPrice), minVal, maxVal);
                
                MelonLogger.Msg($"[HighBaller Mode] Safely setting price to: ${clampedPrice}");
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

            if (offerInfoGO == null)
            {
                Transform targetParent = instance.transform;
                if (instance.Container != null) targetParent = instance.Container.transform;

                CreateLabels(targetParent);
                UpdateSelectorUI(targetParent);
            }
        }

        public static void OnPopupOpen(CounterofferInterface instance)
        {
            if (instance == null) return;
            offerInterface = instance;
            EnsureUIInitialized(instance);

            Customer currCustomer = (instance.conversation != null && instance.conversation.sender != null) 
                ? instance.conversation.sender.GetComponent<Customer>() 
                : null;

            if (currCustomer != null && instance.PriceSelector != null)
            {
                float maxSpend = CalculateSpendingLimits(currCustomer);
                if (maxSpend > 0)
                {
                    SetPriceSafely(instance, maxSpend);
                }
            }

            float currentPrice = (instance.PriceSelector != null) ? instance.PriceSelector.SelectedAmount : 0f;

            if (!CounterOfferConfig.disableAllLabels)
            {
                if (!CounterOfferConfig.disableInitialOffer)
                {
                    if (CounterOfferConfig.enablePricePerUnit && instance.quantity > 0)
                    {
                        SetInitialPriceText(currentPrice / instance.quantity, true);
                        SetFairPriceText(currentPrice / instance.quantity);
                    }
                    else
                    {
                        SetInitialPriceText(currentPrice);
                    }
                }

                if (!CounterOfferConfig.disableMaxLimit && currCustomer != null)
                {
                    float maxSpend = CalculateSpendingLimits(currCustomer);
                    SetMaxCashText(maxSpend);
                }

                if (!CounterOfferConfig.disableSuccessRate && currCustomer != null)
                {
                    float successChance = CalculateSuccessProbability(currCustomer, instance.selectedProduct, instance.quantity, currentPrice);
                    SetSuccessRateText(successChance);
                }
            }
        }

        public static void SetSuccessRateText(float success)
        {
            if (successRateText == null) return;
            successRateText.text = $"<b>{Mathf.RoundToInt(success * 100)}% Chance of Success</b>";
            if (colorMap != null && colorMap.colorKeys != null && colorMap.colorKeys.Length > 0)
            {
                successRateText.color = colorMap.Evaluate(success);
            }
            else
            {
                successRateText.color = Color.green;
            }
        }

        public static void SetMaxCashText(float maxSpend)
        {
            if (maxCashText == null) return;
            maxCashText.text = $"<b>Spend Limit: ${Mathf.RoundToInt(maxSpend)}</b>";
        }

        public static void SetInitialPriceText(float initialPrice, bool ppu = false)
        {
            if (initialOfferText == null) return;
            if (ppu)
            {
                initialOfferText.text = $"<b>Initial Offer: ${Mathf.RoundToInt(initialPrice)} per Unit</b>";
                return;
            }
            initialOfferText.text = $"<b>Initial Offer: ${Mathf.RoundToInt(initialPrice)}</b>";
        }

        public static void SetFairPriceText(float fairPrice)
        {
            if (fairPriceText == null) return;
            fairPriceText.text = $"<b>Price: ${Mathf.RoundToInt(fairPrice)} per Unit</b>";
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

        public static void UpdateSuccessRate(CounterofferInterface instance)
        {
            if (instance == null || successRateText == null) return;
            if (instance.conversation == null || instance.conversation.sender == null) return;
            Customer customer = instance.conversation.sender.GetComponent<Customer>();
            if (customer == null) return;

            float currentPrice = instance.PriceSelector != null ? instance.PriceSelector.SelectedAmount : 0f;
            float probability = CalculateSuccessProbability(customer, instance.selectedProduct, instance.quantity, currentPrice);
            SetSuccessRateText(probability);
        }

        public static void InitOnWake()
        {
            Utility.Log("Initializing Counter Offer UI");
        }

        private static void UpdateSelectorUI(Transform parent)
        {
            if (offerInterface != null && offerInterface.ProductSelector != null)
            {
                selectorInterface = offerInterface.ProductSelector;
            }
        }

        public static void TabSelected(Tab selected)
        {
            currTab = selected.id;
            if (selectorInterface != null)
            {
                selectorInterface.RebuildResultsList();
            }
        }

        private static void CreateLabels(Transform parent)
        {
            if (offerInterface != null && offerInterface.TitleLabel != null)
            {
                gameFont = offerInterface.TitleLabel.font;
            }

            offerInfoGO = new GameObject("OfferInformation");
            offerInfoGO.transform.SetParent(parent, false);
            
            var rect = offerInfoGO.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(-280f, 0f);
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(250, 150);

            float startPosition = 40f;
            if (initialOfferText == null && !CounterOfferConfig.disableInitialOffer)
            {
                initialOfferText = CreateLabel(offerInfoGO.transform, "InitialCash", "Initial Offer: $0", new Vector3(0, startPosition, 0));
                startPosition -= 35f;
            }

            if (maxCashText == null && !CounterOfferConfig.disableMaxLimit)
            {
                maxCashText = CreateLabel(offerInfoGO.transform, "MaxCash", "Spend Limit: $0", new Vector3(0, startPosition, 0));
                startPosition -= 35f;
            }

            if (successRateText == null && !CounterOfferConfig.disableSuccessRate)
            {
                successRateText = CreateLabel(offerInfoGO.transform, "SuccessRate", "100% Chance of Success", new Vector3(0, startPosition, 0));
                startPosition -= 35f;
            }
        }

        public static Text CreateLabel(Transform parent, string title, string text, Vector3 localPosition)
        {
            GameObject labelGo = new GameObject(title);
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = localPosition;
            Text textLabel = labelGo.AddComponent<Text>();
            textLabel.text = text;
            textLabel.font = gameFont != null ? gameFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            textLabel.fontSize = 22;
            textLabel.color = Color.white;
            textLabel.alignment = TextAnchor.MiddleRight;
            RectTransform labelRect = labelGo.transform.GetComponent<RectTransform>();
            if (labelRect != null) labelRect.sizeDelta = new Vector2(250, 30);

            return textLabel;
        }
    }
}
