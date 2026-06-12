using System;
using System.Collections.Generic;
using System.Linq;
using New_Tradegy.Library.Models;

namespace New_Tradegy.Library.Trackers
{
    public static class OrderItemTracker
    {
        public static readonly object orderLock = new object();
        public static Dictionary<int, OrderItem> OrderMap = new Dictionary<int, OrderItem>();


        public static void Add(OrderItem item)
        {
            OrderMap[item.m_ordKey] = item;
        }

        public static OrderItem Get(int ordKey)
        {
            OrderMap.TryGetValue(ordKey, out var item);
            return item;
        }

        public static void Remove(int ordKey)
        {
            OrderMap.Remove(ordKey);
        }

        public static bool Exists(int ordKey)
        {
            return OrderMap.ContainsKey(ordKey);
        }

        public static void Clear()
        {
            OrderMap.Clear();
        }

        public static void Update(int ordKey, Action<OrderItem> updater)
        {
            if (OrderMap.ContainsKey(ordKey))
                updater(OrderMap[ordKey]);
        }

        public static OrderItem GetOrderByRowIndex(int rowIndex)
        {
            OrderItem data = null;

            var keyList = OrderItemTracker.OrderMap.Keys.ToList();
            if (rowIndex < 0 || rowIndex >= keyList.Count)
                return data;

            int key = keyList[rowIndex];
            data = OrderItemTracker.Get(key);
            return data;
        }
    }
}

