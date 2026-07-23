using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System;
using System.Collections.Generic;
#if IL2CPP
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Economy;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using ScheduleOne.Product;
using ScheduleOne.UI.Phone;
using ScheduleOne.UI;
using ScheduleOne.Economy;
using GenericCol = System.Collections.Generic;
#endif

namespace BetterCounterOffer
{
    [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.Open))]
    static class CounterOfferInterfaceOpenPatch
    {
        public static void Postfix(CounterofferInterface __instance)
        {
            if (__instance != null)
            {
                CounterOfferUI.OnPopupOpen(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(AmountSelector), nameof(AmountSelector.ChangeAmount))]
    static class CounterOfferInterfaceChangePricePatch
    {
        public static void Postfix(AmountSelector __instance)
        {
            if (CounterOfferUI.isUpdatingPrice) return;

            if (CounterOfferUI.offerInterface != null && CounterOfferUI.offerInterface.PriceSelector == __instance)
            {
                if (!CounterOfferConfig.disableSuccessRate)
                {
                    CounterOfferUI.UpdateSuccessRate(CounterOfferUI.offerInterface);
                }

                if (CounterOfferConfig.enablePricePerUnit && CounterOfferUI.offerInterface.quantity > 0)
                {
                    CounterOfferUI.SetFairPriceText(__instance.SelectedAmount / CounterOfferUI.offerInterface.quantity);
                }
            }
        }
    }

    [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.ChangeQuantity), new Type[] { typeof(float) })]
    static class CounterOfferInterfaceChangeQuantityPatch
    {
        public static bool Prefix(CounterofferInterface __instance, ref float change)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                int current = __instance.quantity;
                int target = current;

                if (change > 0)
                {
                    target = ((current / 5) + 1) * 5;
                }
                else if (change < 0)
                {
                    if (current <= 5)
                    {
                        target = 1;
                    }
                    else
                    {
                        target = ((current - 1) / 5) * 5;
                    }
                }
                else
                {
                    return true;
                }

                target = Math.Max(1, Math.Min(9999, target));
                change = target - current;
            }

            return true;
        }

        public static void Postfix(CounterofferInterface __instance)
        {
            if (CounterOfferUI.isUpdatingPrice) return;

            if (__instance != null && __instance.PriceSelector != null && __instance.conversation != null && __instance.conversation.sender != null)
            {
                Customer customer = __instance.conversation.sender.GetComponent<Customer>();
                if (customer != null)
                {
                    float maxSpend = CounterOfferUI.CalculateSpendingLimits(customer);
                    if (maxSpend > 0)
                    {
                        CounterOfferUI.SetPriceSafely(__instance, maxSpend);
                    }
                }
            }

            if (!CounterOfferConfig.disableSuccessRate)
            {
                CounterOfferUI.UpdateSuccessRate(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.SetProduct))]
    static class CounterOfferInterfaceSetProductPatch
    {
        public static void Postfix(CounterofferInterface __instance)
        {
            if (CounterOfferUI.isUpdatingPrice) return;

            if (__instance != null && __instance.PriceSelector != null && __instance.conversation != null && __instance.conversation.sender != null)
            {
                Customer customer = __instance.conversation.sender.GetComponent<Customer>();
                if (customer != null)
                {
                    float maxSpend = CounterOfferUI.CalculateSpendingLimits(customer);
                    if (maxSpend > 0)
                    {
                        CounterOfferUI.SetPriceSafely(__instance, maxSpend);
                    }
                }
            }

            if (!CounterOfferConfig.disableSuccessRate)
            {
                CounterOfferUI.UpdateSuccessRate(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.UpdateFairPrice))]
    static class CounterofferInterface_UpdateFairPrice_Patch
    {
        public static void Postfix(CounterofferInterface __instance)
        {
            if (CounterOfferConfig.enablePricePerUnit && __instance.PriceSelector != null)
            {
                CounterOfferUI.SetFairPriceText(__instance.PriceSelector.SelectedAmount);
            }
        }
    }

    [HarmonyPatch(typeof(CounterOfferProductSelector), nameof(CounterOfferProductSelector.Open))]
    static class CounterOfferProductSelectorOpenPatch
    {
        public static void Postfix(CounterOfferProductSelector __instance)
        {
            if (CounterOfferUI.selectorInterface == null)
            {
                CounterOfferUI.selectorInterface = __instance;
            }
        }
    }

    [HarmonyPatch(typeof(CounterOfferProductSelector), nameof(CounterOfferProductSelector.GetMatchingProducts))]
    static class CounterOfferProductSelectorGetMatchingProductsPatch
    {
        public static void Postfix(CounterofferInterface __instance, ref GenericCol.List<ProductDefinition> __result, ref string searchTerm)
        {
            HashSet<EDrugType> drugTypes = new HashSet<EDrugType>();
            GenericCol.List<ProductDefinition> lp;
            if (CounterOfferUI.currTab == "Listed")
            {
                lp = ProductManager.ListedProducts;
            }
            else if (CounterOfferUI.currTab == "Favorites")
            {
                lp = ProductManager.FavouritedProducts;
            }
            else
            {
                lp = ProductManager.DiscoveredProducts;
            }
            GenericCol.List<ProductDefinition> newList = new GenericCol.List<ProductDefinition>();
            if (searchTerm.ToLower().Contains("weed")) { drugTypes.Add(EDrugType.Marijuana); }
            if (searchTerm.ToLower().Contains("coke")) { drugTypes.Add(EDrugType.Cocaine); }
            if (searchTerm.ToLower().Contains("meth")) { drugTypes.Add(EDrugType.Methamphetamine); }

            foreach (ProductDefinition p in lp)
            {
                if (drugTypes.Contains(p.DrugType) || p.Name.ToLower().Contains(searchTerm))
                {
                    newList.Add(p);
                }
            }

            __result = newList;
        }
    }
}
