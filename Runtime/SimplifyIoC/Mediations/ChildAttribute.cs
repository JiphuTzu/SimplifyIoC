using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

/*
 * @author  JiphuTzu
 * @date    2021/11/4
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
 *              private void Awake(){
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

    public ChildAttribute(string path, bool includeParent)
    {
        this.path = path;
        this.includeParent = includeParent;
    }
}

public static class ChildAttributeExtension
{
    private enum ValueType : byte
    {
        None,
        GameObjectSingle,
        ComponentSingle,
        GameObjectArray,
        ComponentArray,
        GameObjectList,
        ComponentList
    }

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
            var fieldType = field.FieldType;
            var valueType = GetValueType(fieldType);
            //Debug.Log($"========={field.Name} == {fieldType}");
            if (valueType == ValueType.None) continue;
            //是否已经赋值
            if (HasValue(valueType, field, target)) continue;
            //根据路径查找对象
            var transform = target.transform;
            if (!string.IsNullOrEmpty(attribute.path)) transform = transform.Find(attribute.path);
            else if (attribute.sameAsField) transform = GetChild(transform, field.Name.ToLower());

            if (transform == null) continue;

            //赋值
            if (valueType == ValueType.GameObjectSingle) field.SetValue(target, transform.gameObject);
            else if (valueType == ValueType.ComponentSingle) field.SetValue(target, transform.GetComponent(fieldType));
            else if (valueType == ValueType.GameObjectArray)
                field.SetValue(target, GetGameObjects(transform, attribute.includeParent).ToArray());
            else if (valueType == ValueType.ComponentArray)
                field.SetValue(target,
                    GetComponents(transform, attribute.includeParent, fieldType.GetElementType(), true));
            else if (valueType == ValueType.GameObjectList)
                field.SetValue(target, GetGameObjects(transform, attribute.includeParent));
            else if (valueType == ValueType.ComponentList)
                field.SetValue(target,
                    GetComponents(transform, attribute.includeParent, fieldType.GetGenericArguments()[0]));
        }
    }

    private static List<GameObject> GetGameObjects(Transform parent, bool includeParent)
    {
        var list = new List<GameObject>();
        if (includeParent) list.Add(parent.gameObject);
        foreach (Transform child in parent)
        {
            list.Add(child.gameObject);
        }

        return list;
    }

    private static object GetComponents(Transform parent, bool includeParent, Type elementType, bool asArray = false)
    {
        var list = Activator.CreateInstance(_TOL.MakeGenericType(elementType));
        var add = list.GetType().GetMethod("Add");
        Component element = null;
        if (includeParent)
        {
            element = parent.GetComponent(elementType);
            if (element != null) add.Invoke(list, new object[] {element});
        }

        foreach (Transform child in parent)
        {
            element = child.GetComponent(elementType);
            if (element != null) add.Invoke(list, new object[] {element});
        }

        if (!asArray) return list;
        //
        var toArray = list.GetType().GetMethod("ToArray");
        return toArray.Invoke(list, new object[] { });
    }

    private static Transform GetChild(Transform parent, string name)
    {
        //去掉私有变量前的下划线
        while (name.StartsWith("_"))
        {
            name = name.Substring(1);
        }

        if (parent.name.ToLower() == name) return parent;
        for (int i = 0, count = parent.childCount; i < count; i++)
        {
            var child = GetChild(parent.GetChild(i), name);
            if (child != null) return child;
        }

        return null;
    }

    private static readonly Type _TOC = typeof(Component);
    private static readonly Type _TOG = typeof(GameObject);
    private static readonly Type _TOL = typeof(List<>);

    private static ValueType GetValueType(Type fieldType)
    {
        //Debug.Log($"{fieldType.Name} has element type: {fieldType.HasElementType}");
        if (!fieldType.HasElementType)
        {
            if (fieldType == _TOG) return ValueType.GameObjectSingle;
            if (fieldType.IsSubclassOf(_TOC)) return ValueType.ComponentSingle;

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == _TOL)
            {
                fieldType = fieldType.GetGenericArguments()[0];
                if (fieldType == _TOG) return ValueType.GameObjectList;
                if (fieldType.IsSubclassOf(_TOC)) return ValueType.ComponentList;
            }
        }
        else
        {
            //数组
            fieldType = fieldType.GetElementType();
            if (fieldType == _TOG) return ValueType.GameObjectArray;
            if (fieldType.IsSubclassOf(_TOC)) return ValueType.ComponentArray;
        }

        return ValueType.None;
    }

    private static bool HasValue(ValueType type, FieldInfo field, MonoBehaviour target)
    {
        //GameObject数组
        var value = field.GetValue(target);
        //TODO：当类型为Transform或者RectTransform时，value的值会是"null"
        if (value == null || "" + value == "null") return false;

        if (type == ValueType.GameObjectArray || type == ValueType.ComponentArray)
            return (value as Array).Length > 0;

        if (type == ValueType.GameObjectList || type == ValueType.ComponentList)
            return ((IList) value).Count > 0;

        return true;
    }
}
