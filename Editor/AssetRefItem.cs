using System;
using UnityEngine;
using UnityEngine.UIElements;




public class AssetRefItem : VisualElement
{
    public Image Img { get; private set; }
    public Label NameLabel { get; private set; }
    public Action OnClick;

    public static AssetRefItem CreateItem(string name, Texture2D texture)
    {
        return new AssetRefItem(name, texture);
    }

    public AssetRefItem(string name, Texture2D texture)
    {
        Init(name, texture);
    }

    private void Init(string name, Texture2D texture2D)
    {
        this.AddManipulator(new Clickable(OnClicked));

        Img = new Image();
        Img.sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), Vector2.zero);
        Add(Img);

        NameLabel = new Label(name);
        Add(NameLabel);
    }

    private void OnClicked()
    {
        OnClick?.Invoke();
    }
}
