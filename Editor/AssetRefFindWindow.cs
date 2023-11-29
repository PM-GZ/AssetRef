using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class AssetRefFindWindow : EditorWindow
{
    public enum RefType
    {
        None,
        NotRefTo,
        NotRefFrom,
        NotRef,
    }


    private VisualElement mRoot;
    private VisualElement mContentContainer;
    private VisualElement mLeftContainer;
    private VisualElement mRightContainer;
    private Label mReferenceToCountLabel;
    private ScrollView mReferenceToScroll;
    private ScrollView mReferenceFromScroll;
    private Label mReferenceFromCountLabel;
    private ToolbarSearchField mFileSearch;
    private ScrollView mMainAssetScroll;
    private ObjectField mSearchFloder;
    private Label mMainAssetCount;


    private AssetRefManager mAssetManager;
    private VisualElement mSelectedItem;
    private bool mOneClick;
    private double mOneClickTime;
    private Toggle mFilterUnity;
    private TextField mIgnoreFloder;
    private DropdownField mFileExtend;
    private EnumField mRefType;

    [MenuItem("Tools/资源引用查找")]
    private static void ShowWindow()
    {
        var wnd = GetWindow<AssetRefFindWindow>();
        wnd.titleContent = new GUIContent("资产引用查找");
        wnd.Show();
    }

    private void CreateGUI()
    {
        mAssetManager = new();
        mRoot = rootVisualElement;
        mRoot.style.flexDirection = FlexDirection.Column;
        mRoot.styleSheets.Add(Resources.Load<StyleSheet>("Styles/AssetReferenceFindStyle"));

        Init();
    }

    private void Init()
    {
        mSelectedItem = null;

        mRoot.Clear();
        CreateToolbar();
        mContentContainer = new VisualElement();
        mContentContainer.style.flexDirection = FlexDirection.Row;
        mRoot.Add(mContentContainer);

        mLeftContainer = CreateLeftContianer();
        mLeftContainer.RegisterCallback<GeometryChangedEvent>(OnLeftWidthChanged);
        mRightContainer = CreateRightContianer();
        mRightContainer.style.display = DisplayStyle.None;

        mContentContainer.Add(SplitView.CreateHorizontalSplitView(mLeftContainer, mRightContainer));
    }

    #region Create Layout
    private void CreateToolbar()
    {
        Toolbar toolbarMenu = new Toolbar();
        mRoot.Add(toolbarMenu);

        Button delNotRef = new Button(OnDelNotRefClick);
        delNotRef.name = "delNotRef-btn";
        delNotRef.text = "删除未使用资源";
        toolbarMenu.Add(delNotRef);
    }

    private VisualElement CreateLeftContianer()
    {
        VisualElement left = new VisualElement();
        left.name = "left-container";

        Label title = new Label("主资源");
        title.AddToClassList("left-title");
        left.Add(title);

        mSearchFloder = new ObjectField("目标文件夹");
        mSearchFloder.name = "search-floder";
        mSearchFloder.allowSceneObjects = false;
        mSearchFloder.objectType = typeof(DefaultAsset);
        mSearchFloder.RegisterValueChangedCallback(OnSearchFloderValueChanged);
        left.Add(mSearchFloder);
        mSearchFloder.value = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");

        mMainAssetCount = new Label();
        mMainAssetCount.name = "left-main-asset-count";
        left.Add(mMainAssetCount);

        mFileExtend = new DropdownField("文件类型", mAssetManager.GetFileExtends(), "*");
        mFileExtend.name = "file-extend";
        mFileExtend.RegisterValueChangedCallback(OnFileExtendValueChanged);
        left.Add(mFileExtend);

        mRefType = new EnumField(RefType.None);
        mRefType.name = "ref-type";
        mRefType.RegisterValueChangedCallback(OnRefTypeValueChanged);
        mAssetManager.CurRefType = RefType.None;
        left.Add(mRefType);

        mFileSearch = new ToolbarSearchField();
        mFileSearch.name = "file-search";
        mFileSearch.RegisterValueChangedCallback(OnFileSearchValueChanged);
        left.Add(mFileSearch);

        mMainAssetScroll = new ScrollView(ScrollViewMode.Vertical);
        mMainAssetScroll.name = "main-asset-list";
        left.Add(mMainAssetScroll);

        UpdateMainAssetItems(mAssetManager.GetRefTo().Keys.ToList());

        return left;
    }

    private VisualElement CreateRightContianer()
    {
        VisualElement right = new VisualElement();
        right.name = "right-container";

        {
            VisualElement top = new VisualElement();
            top.name = "top-container";
            right.Add(top);

            mFilterUnity = new Toggle("忽略文件夹");
            mFilterUnity.name = "ignore-floder-toggle";
            mFilterUnity.RegisterValueChangedCallback(OnFilterUnityValueChanged);
            top.Add(mFilterUnity);

            mIgnoreFloder = new TextField();
            mIgnoreFloder.name = "ignore-floder";
            mIgnoreFloder.isDelayed = true;
            mIgnoreFloder.RegisterValueChangedCallback(OnFilterFloderValueChanged);
            top.Add(mIgnoreFloder);
            mIgnoreFloder.value = EditorPrefs.GetString("IgnoreFloder", "");
        }

        {
            mReferenceToCountLabel = new Label("引用");
            mReferenceToCountLabel.name = "reference-label";
            mReferenceToScroll = new ScrollView(ScrollViewMode.Vertical);
            right.Add(mReferenceToCountLabel);
            right.Add(mReferenceToScroll);

            mReferenceFromCountLabel = new Label("被引用");
            mReferenceFromCountLabel.name = "reference-label";
            mReferenceFromScroll = new ScrollView(ScrollViewMode.Vertical);
            right.Add(mReferenceFromCountLabel);
            right.Add(mReferenceFromScroll);
        }

        return right;
    }
    #endregion

    private void UpdateMainAssetItems(List<string> guids)
    {
        mMainAssetCount.text = $"资源数：{guids.Count}";
        mMainAssetScroll.contentContainer.Clear();
        foreach (var guid in guids)
        {
            AddLeftItem(guid);
        }
    }

    private void AddLeftItem(string guid)
    {
        VisualElement item = null;
        item = CreateItem(guid, () => { OnItemSelected(item); OnMainAssetClick(guid); });
        mMainAssetScroll.contentContainer.Add(item);
    }

    private VisualElement CreateItem(string guid, Action action)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        var texture2D = AssetPreview.GetMiniThumbnail(asset);
        texture2D ??= AssetPreview.GetMiniTypeThumbnail(asset.GetType());

        VisualElement item = new VisualElement();
        item.AddToClassList("list-item");
        item.AddManipulator(new Clickable(action));

        Image img = new Image();
        img.AddToClassList("list-item-img");
        img.sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), Vector2.zero);
        item.Add(img);

        Label label = new Label(asset.name);
        label.AddToClassList("list-item-label");
        item.Add(label);

        return item;
    }

    private void SetDoubleClickData(string guid, double time, bool click)
    {
        mAssetManager.CurSelectAsset = guid;
        mOneClickTime = time;
        mOneClick = click;
    }

    private double GetTotalSeconds(long ticks)
    {
        TimeSpan span = TimeSpan.FromTicks(ticks);
        return span.TotalSeconds;
    }

    private void UpdateRightBox(string guid)
    {
        if (mFilterUnity.value)
        {
            UpdateFilterUnityReferenceAsset();
        }
        else
        {
            UpdateReferenceScrollList(mReferenceToScroll, mAssetManager.GetRefToValue(guid));
            List<string> list = new List<string>();
            if (mAssetManager.TryGetRefFromValue(guid, out var v))
                list = v;
            UpdateReferenceScrollList(mReferenceFromScroll, list);
            UpdateInfoBox(mAssetManager.GetRefToValue(guid).Count, list.Count);
        }
    }

    private void UpdateReferenceScrollList(ScrollView scroll, List<string> list)
    {
        scroll.contentContainer.Clear();
        foreach (var value in list)
        {
            var item = CreateItem(value, () => { PingObject(value); });
            scroll.contentContainer.Add(item);
        }
    }

    private void UpdateInfoBox(int toCount, int fromCount)
    {
        mReferenceToCountLabel.text = $"引用：{toCount}";
        mReferenceFromCountLabel.text = $"被引用：{fromCount}";
    }

    private void PingObject(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        EditorGUIUtility.PingObject(asset);
    }

    private void UpdateFilterUnityReferenceAsset()
    {
        (List<string> to, List<string> from) data = mAssetManager.GetFilterAssets();

        UpdateInfoBox(data.to.Count, data.from.Count);
        UpdateReferenceScrollList(mReferenceToScroll, data.to);
        UpdateReferenceScrollList(mReferenceFromScroll, data.from);
    }

    #region action
    private void OnDelNotRefClick()
    {
        mAssetManager.DelNotRef(Init);
    }

    private void OnLeftWidthChanged(GeometryChangedEvent evt)
    {
        mAssetManager.SetLeftWidth(evt.newRect.width);
    }

    private void OnFileExtendValueChanged(ChangeEvent<string> evt)
    {
        var list = mAssetManager.GetFilterResult(evt.newValue, (RefType)mRefType.value);
        UpdateMainAssetItems(list);
    }

    private void OnRefTypeValueChanged(ChangeEvent<Enum> evt)
    {
        var list = mAssetManager.GetFilterResult(mFileExtend.value, (RefType)mRefType.value);
        UpdateMainAssetItems(list);
    }

    private void OnFileSearchValueChanged(ChangeEvent<string> evt)
    {
        mMainAssetScroll.contentContainer.Clear();
        var list = mAssetManager.GetFileSearchValue(evt.newValue);
        UpdateMainAssetItems(list);
    }

    private void OnMainAssetClick(string guid)
    {
        if (mAssetManager.CurSelectAsset == guid && mOneClick && GetTotalSeconds(DateTime.Now.Ticks) - mOneClickTime < 0.5f)
        {
            PingObject(guid);
            SetDoubleClickData(null, 0, false);
        }
        else
        {
            SetDoubleClickData(guid, GetTotalSeconds(DateTime.Now.Ticks), true);
            UpdateRightBox(guid);
            mRightContainer.style.display = DisplayStyle.Flex;
        }
    }

    private void OnFilterFloderValueChanged(ChangeEvent<string> evt)
    {
        EditorPrefs.SetString("IgnoreFloder", evt.newValue);
        mAssetManager.UpdateIgnoreFloder();
        if (mFilterUnity.value)
        {
            UpdateFilterUnityReferenceAsset();
        }
    }

    private void OnFilterUnityValueChanged(ChangeEvent<bool> evt)
    {
        if (evt.newValue)
        {
            UpdateFilterUnityReferenceAsset();
        }
        else
        {
            UpdateRightBox(mAssetManager.CurSelectAsset);
        }
    }

    private void OnItemSelected(VisualElement item)
    {
        if (mSelectedItem != null)
        {
            ColorUtility.TryParseHtmlString("#656565", out Color c);
            mSelectedItem.style.backgroundColor = new StyleColor(c);
        }
        mSelectedItem = item;
        ColorUtility.TryParseHtmlString("#00C6FF", out Color color);
        mSelectedItem.style.backgroundColor = new StyleColor(color);
    }

    private void OnSearchFloderValueChanged(ChangeEvent<UnityEngine.Object> evt)
    {
        var value = evt.newValue as DefaultAsset;
        string path = AssetDatabase.GetAssetPath(value);
        var list = mAssetManager.GetSearchFloderValues(path);
        mFileExtend.choices = mAssetManager.GetFileExtends();
        UpdateMainAssetItems(list);
    }
    #endregion
}
