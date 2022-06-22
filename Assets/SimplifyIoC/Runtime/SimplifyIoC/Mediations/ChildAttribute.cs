/*
 * @file    ChildAttributeExtension.cs
 * @author  JiphuTzu
 * @date    2021/11/4
 * 	
 * @version  1.0
 *
 * @brief	（简要描述）
 *
 * @details	公司：Umawerse
 *			对该文档的详细说明和解释，可以换行
 * @see     （参见可添加URL地址）
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

/*
 * @brief	用于变量和子对象的绑定
 * @usage
 *          public Test:MonoBehaviour
 *          {
 *              [Child]
 *              public Image image;
 *              [Child("submit")]
 *              public Button submit;
 *              [Child("content/items")]
 *              private GameObject[] _items;
 *              [Child("content/icons")]
 *              public List<RawImage> images;
 * 
 *              private void Start(){
 *                  this.MapChildren();
 *              }
 *           }
 */
[AttributeUsage(AttributeTargets.Field)]
public class ChildAttribute : PreserveAttribute
{
    public string path;
    public bool includeParent;
    //名字与变量名相同，在path为空的情况下有效
    public bool sameAsField;

    // The class constructor is called when the class instance is created
    public ChildAttribute()
    {
    }

    public ChildAttribute(string path)
    {
        this.path = path;
    }

    public ChildAttribute(bool sameAsField)
    {
        this.sameAsField = sameAsField;
    }
    //用于数组和List
    public ChildAttribute(string path, bool includeParent)
    {
        this.path = path;
        this.includeParent = includeParent;
    }
}

public static class ChildAttributeExtension
{
    /// <summary>
    /// 把带有ChildAttribute的变量与子对象对应。
    /// 支持公有变量和私有变量
    /// </summary>
    /// <param name="target"></param>
    public static void MapChildren(this MonoBehaviour target)
    {
        var type = target.GetType();
        var fields = type.GetFields(BindingFlags.Instance
                                    | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            //查找变量是否带有ChildAttribute
            var attribute = field.GetCustomAttribute<ChildAttribute>();
            if (attribute == null) continue;
            //变量类型要是GameObject或者Component的子类
            var ft = GetFieldType(field);
            //Debug.Log($"========={field.Name} == {ft}");
            if (ft == -1) continue;
            //是否已经赋值
            if (HasValue(ft, field, target)) continue;
            //根据路径查找对象
            var t = string.IsNullOrEmpty(attribute.path) 
                ? (attribute.sameAsField ? GetChild(target.transform,field.Name.ToLower()) : target.transform)
                : target.transform.Find(attribute.path);
            
            if (t == null) continue;

            //赋值
            if (ft == 0) field.SetValue(target, t.gameObject);
            else if (ft == 1) field.SetValue(target, t.GetComponent(field.FieldType));
            else if (ft == 2 || ft == 4)
            {
                var list = new List<GameObject>();
                if (attribute.includeParent) list.Add(t.gameObject);
                foreach (Transform c in t)
                {
                    list.Add(c.gameObject);
                }

                if (ft == 2)
                    field.SetValue(target, list.ToArray());
                else //if(ft == 4) 
                    field.SetValue(target, list);
            }
            else if (ft == 3 || ft == 5)
            {
                var et = ft == 3 ? field.FieldType.GetElementType() : field.FieldType.GetGenericArguments()[0];
                var list = Activator.CreateInstance(_TOL.MakeGenericType(et));
                var add = list.GetType().GetMethod("Add");
                Component element = null;
                if (attribute.includeParent)
                {
                    element = t.GetComponent(et);
                    if (element != null) add.Invoke(list, new object[] {element});
                }

                foreach (Transform c in t)
                {
                    element = c.GetComponent(et);
                    if (element != null) add.Invoke(list, new object[] {element});
                }

                if (ft == 3)
                {
                    var toArray = list.GetType().GetMethod("ToArray");
                    field.SetValue(target, toArray.Invoke(list, new object[] { }));
                }
                else // if (ft == 5)
                {
                    field.SetValue(target, list);
                }
            }
        }
    }
    
    private static Transform GetChild(Transform parent, string name)
    {
        if (parent.name.ToLower() == name) return parent;
        for (int i = 0,count = parent.childCount; i < count; i++)
        {
            var child = GetChild(parent.GetChild(i), name);
            if (child != null) return child;
        }

        return null;
    }

    private static readonly Type _TOC = typeof(Component);
    private static readonly Type _TOG = typeof(GameObject);
    private static readonly Type _TOL = typeof(List<>);

    private static int GetFieldType(FieldInfo field)
    {
        var fieldType = field.FieldType;
        //Debug.Log($"{fieldType.Name} has element type: {fieldType.HasElementType}");
        if (!fieldType.HasElementType)
        {
            if (fieldType == _TOG) return 0;
            if (fieldType.IsSubclassOf(_TOC)) return 1;

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == _TOL)
            {
                fieldType = fieldType.GetGenericArguments()[0];
                if (fieldType == _TOG) return 4;
                if (fieldType.IsSubclassOf(_TOC)) return 5;
            }
        }
        else
        {
            //数组
            fieldType = fieldType.GetElementType();
            if (fieldType == _TOG) return 2;
            if (fieldType.IsSubclassOf(_TOC)) return 3;
        }

        return -1;
    }

    private static bool HasValue(int type, FieldInfo field, MonoBehaviour target)
    {
        //GameObject数组
        var value = field.GetValue(target);
        if (type == 2 || type == 3) return (value as Array).Length > 0;
        if (type == 4 || type == 5) return ((IList) value).Count > 0;
        
        //TODO：当类型为Transform或者RectTransform时，value的值会是"null"
        return value != null && ""+value != "null";
    }
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void InitializeOnApplicationLoad()
    {
        OnSelectionChanged();
        Selection.selectionChanged += OnSelectionChanged;
    }

    private static void OnSelectionChanged()
    {
        var selected = Selection.activeGameObject;
        if (selected == null) return;
        var monos = selected.GetComponentsInChildren<MonoBehaviour>();
        foreach (var mono in monos)
        {
            mono.MapChildren();
        }
    }
#endif
}