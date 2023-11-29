using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using static AssetRefFindWindow;



public class AssetRefManager
{
    private string[] mIgnoreFloder;
    private HashSet<string> mFileExtends = new HashSet<string>();
    private Dictionary<string, List<string>> mReferenceTo = new();
    private Dictionary<string, List<string>> mReferenceFrom = new();
    public int RefToCount { get => mReferenceTo.Count; }
    public int RefFromCount { get => mReferenceFrom.Count; }
    public string Extend { get; private set; } = "*";
    public string SearchFloder { get; private set; } = "Assets";

    public RefType CurRefType = RefType.None;
    public string CurSelectAsset;
    private float mLeftWidth;
    private string[] mAllAssets;

    public AssetRefManager()
    {
        UpdateIgnoreFloder();
        Selection.activeObject = null;
        CurSelectAsset = null;
        mReferenceTo.Clear();
        mReferenceFrom.Clear();
        mFileExtends.Clear();
        CurRefType = RefType.None;
        FindGameAssetReference();
    }

    private void FindGameAssetReference()
    {
        mAllAssets ??= AssetDatabase.FindAssets("t:Object", new string[] { "Assets" });
        List<string> assetGUIDs = AssetDatabase.FindAssets("t:Object", new string[] { SearchFloder }).ToList();
        mFileExtends.Add("*");

        FindAssetReference(assetGUIDs);
    }

    private void FindAssetReference(List<string> assetGUIDs)
    {
        for (int i = assetGUIDs.Count - 1; i >= 0; i--)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
            string extend = Path.GetExtension(path);
            mFileExtends.Add(extend);
            if (!CheckFileEligible(path))
                assetGUIDs.RemoveAt(i);
        }
        foreach (var guid in assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path))
                continue;

            List<string> allRefs = AssetDatabase.GetDependencies(path, false).Where(val => val != path).ToList();
            mReferenceTo.Add(guid, GetReferenceTo(path, allRefs));
            GetReferenceFrom(guid, path, allRefs);
        }
    }

    private List<string> GetReferenceTo(string path, List<string> allRefs)
    {
        List<string> newList = new List<string>();
        foreach (var item in allRefs)
        {
            if (item == path) continue;
            string id = AssetDatabase.AssetPathToGUID(item);
            newList.Add(id);
        }
        return newList;
    }

    private void GetReferenceFrom(string guid, string path, List<string> allRefs)
    {
        foreach (var reference in allRefs)
        {
            string refGuid = AssetDatabase.AssetPathToGUID(reference);

            if (!mReferenceFrom.ContainsKey(refGuid))
                mReferenceFrom.Add(refGuid, new List<string>());

            mReferenceFrom[refGuid].Add(guid);
        }
    }

    private bool CheckFileEligible(string path)
    {
        foreach (var item in mIgnoreFloder)
        {
            if (!path.StartsWith("Assets") || path.Contains(item))
                return false;
        }
        return true;
    }

    public void SetLeftWidth(float leftWidth)
    {
        mLeftWidth = leftWidth;
    }

    public Dictionary<string, List<string>> GetRefTo()
    {
        return mReferenceTo;
    }

    public Dictionary<string, List<string>> GetRefFrom()
    {
        return mReferenceFrom;
    }

    public List<string> GetRefToValue(string key)
    {
        return mReferenceTo[key];
    }

    public List<string> GetRefFromValue(string key)
    {
        return mReferenceFrom[key];
    }

    public bool TryGetRefToValue(string key, out List<string> value)
    {
        return mReferenceTo.TryGetValue(key, out value);
    }

    public bool TryGetRefFromValue(string key, out List<string> value)
    {
        return mReferenceFrom.TryGetValue(key, out value);
    }

    public List<string> GetFileExtends()
    {
        return mFileExtends.ToList();
    }

    public List<string> GetSearchFloderValues(string floder)
    {
        SearchFloder = floder;
        mReferenceTo.Clear();
        mReferenceFrom.Clear();
        mFileExtends.Clear();
        FindGameAssetReference();
        return GetFilterResult(Extend, CurRefType);
    }

    private List<string> GetRefTypeValue()
    {
        List<string> list = new List<string>();
        switch (CurRefType)
        {
            case RefType.NotRefTo:
                foreach (var kv in mReferenceTo)
                {
                    if (kv.Value.Count == 0)
                        list.Add(kv.Key);
                }
                break;
            case RefType.NotRefFrom:
                foreach (var kv in mReferenceTo)
                {
                    if (!mReferenceFrom.TryGetValue(kv.Key, out var value) || value.Count == 0)
                        list.Add(kv.Key);
                }
                break;
            case RefType.NotRef:
                foreach (var kv in mReferenceTo)
                {
                    if (kv.Value.Count == 0 && (!mReferenceFrom.TryGetValue(kv.Key, out var value) || value.Count == 0))
                    {
                        list.Add(kv.Key);
                    }
                }
                break;
        }
        return list;
    }

    public List<string> GetFileSearchValue(string evt)
    {
        if (string.IsNullOrEmpty(evt))
            return mReferenceTo.Keys.ToList();

        List<string> list = new List<string>();
        foreach (var k in mReferenceTo.Keys)
        {
            string path = AssetDatabase.GUIDToAssetPath(k);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Equals(evt))
            {
                list.Add(k);
                return list;
            }
            else if (name.Contains(evt))
            {
                list.Add(k);
            }
        }
        return list;
    }

    public (List<string>, List<string>) GetFilterAssets()
    {
        (List<string>, List<string>) data = (new List<string>(), new List<string>());
        if (mReferenceTo.TryGetValue(CurSelectAsset, out var value))
        {
            data.Item1 = GetFilterAssets(value);
        }

        if (mReferenceFrom.TryGetValue(CurSelectAsset, out value))
        {
            data.Item2 = GetFilterAssets(value);
        }
        return data;
    }

    private List<string> GetFilterAssets(List<string> data)
    {
        var list = new List<string>();
        foreach (var v in data)
        {
            string path = AssetDatabase.GUIDToAssetPath(v);
            if (CheckFileEligible(path))
            {
                list.Add(v);
            }
        }
        return list;
    }

    public void UpdateIgnoreFloder()
    {
        string ignore = EditorPrefs.GetString("IgnoreFloder", "");
        mIgnoreFloder = ignore.Split(',');
    }

    private List<string> GetFileExtentAssets()
    {
        List<string> list = new List<string>();
        foreach (var k in mReferenceTo.Keys)
        {
            string path = AssetDatabase.GUIDToAssetPath(k);
            string ex = Path.GetExtension(path);
            if (ex.Equals(Extend))
                list.Add(k);
        }
        return list;
    }

    public List<string> GetFilterResult(string extend, RefType refType)
    {
        Extend = extend;
        CurRefType = refType;
        if (CurRefType == RefType.None && Extend.Equals("*"))
        {
            return mReferenceTo.Keys.ToList();
        }
        else if (CurRefType == RefType.None && !Extend.Equals("*"))
        {
            return GetFileExtentAssets();
        }
        else if (CurRefType != RefType.None && Extend.Equals("*"))
        {
            return GetRefTypeValue();
        }
        else
        {
            var list = GetRefTypeValue();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string path = AssetDatabase.GUIDToAssetPath(list[i]);
                string ex = Path.GetExtension(path);
                if (!ex.Equals(Extend))
                    list.RemoveAt(i);
            }
            return list;
        }
    }

    public void DelNotRef(Action action)
    {
        string delFail = string.Empty;
        List<string> delAssets = new List<string>();
        foreach (var kv in mReferenceTo)
        {
            if (kv.Value.Count == 0)
            {
                delAssets.Add(kv.Key);
            }
        }
        foreach (var guid in delAssets)
        {
            if (!mReferenceFrom.TryGetValue(guid, out var list) || list.Count == 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.DeleteAsset(path))
                {
                    delFail += $"{delFail}\n";
                }
            }
        }
        EditorApplication.delayCall += () =>
        {
            string msg = string.IsNullOrEmpty(delFail) ? "删除成功" : delFail;
            if (EditorUtility.DisplayDialog("提示", msg, "确认"))
            {
                action?.Invoke();
            }
        };
        AssetDatabase.Refresh();
    }
}
