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
            lock (orderLock)
            {
                OrderMap[item.m_ordKey] = item;
            }
        }

        public static OrderItem Get(int ordKey)
        {
            lock (orderLock)
            {
                OrderMap.TryGetValue(ordKey, out var item);
                return item;
            }
        }

        public static void Remove(int ordKey)
        {
            lock (orderLock)
            {
                OrderMap.Remove(ordKey);
            }
        }

        public static bool Exists(int ordKey)
        {
            lock (orderLock)
            {
                return OrderMap.ContainsKey(ordKey);
            }
        }

        public static void Clear()
        {
            lock (orderLock)
            {
                OrderMap.Clear();
            }
        }

        public static void Update(int ordKey, Action<OrderItem> updater)
        {
            lock (orderLock)
            {
                if (OrderMap.ContainsKey(ordKey))
                    updater(OrderMap[ordKey]);
            }
        }
        public static OrderItem GetOrderByRowIndex(int rowIndex)
        {
            lock (orderLock)
            {
                var keyList = OrderMap.Keys.ToList();

                if (rowIndex < 0 || rowIndex >= keyList.Count)
                    return null;

                return OrderMap[keyList[rowIndex]];
            }
        }
    }
}

