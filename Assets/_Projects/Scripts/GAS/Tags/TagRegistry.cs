using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    /// <summary>
    /// 标签注册表，自动维护字符串与ID的映射
    /// </summary>
    public static class TagRegistry
    {
        private static readonly Dictionary<string, int> TagToId = new Dictionary<string, int>();
        private static readonly Dictionary<int, string> IdToTag = new Dictionary<int, string>();
        private static readonly Dictionary<int, HashSet<int>> ParentChildMap = new Dictionary<int, HashSet<int>>();
        
        private static int nextId = 1; // 0 表示无效

        //注册标签
        public static int Register(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return 0;

            if (TagToId.TryGetValue(tagName, out int existingId))
                return existingId;

            int newId = nextId++;
            TagToId[tagName] = newId;
            IdToTag[newId] = tagName;

            RegisterHierarchy(tagName, newId);

            return newId;
        }

        /// <summary>
        /// 解析层级关系，建立父子图谱
        /// </summary>
        private static void RegisterHierarchy(string tagName, int childId)
        {
            int lastDot = tagName.LastIndexOf('.');
            if (lastDot > 0)
            {
                string parentName = tagName.Substring(0, lastDot);
                int parentId = Register(parentName); //递归注册父级
                
                if (!ParentChildMap.ContainsKey(parentId))
                    ParentChildMap[parentId] = new HashSet<int>();
                ParentChildMap[parentId].Add(childId);
            }
        }

        /// <summary>
        /// 判断是否是子标签
        /// </summary>
        public static bool IsChildOf(int childId, int parentId)
        {
            if (childId == 0 || parentId == 0) return false;
            if (childId == parentId) return true;

            //查询父级
            if (ParentChildMap.TryGetValue(parentId, out var children))
            {
                if (children.Contains(childId)) return true;
                //注意：这里只检查直接父级
            }
            return false;
        }

        //调试用
        //根据id获取标签名字
        //TODO:注释掉这个函数
        public static string GetTagName(int id) => IdToTag.TryGetValue(id, out var name) ? name : "Unknown";
    }
