using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


public class SplitView : VisualElement
{
    public VisualElement FixedPanel;
    private VisualElement mFlexedPanel;
    private VisualElement mDragLine;
    public MouseCursor CursorDirection;
    public static SplitView CreateHorizontalSplitView(VisualElement left, VisualElement right)
    {
        return new SplitView(left, right, MouseCursor.ResizeHorizontal);
    }

    public static SplitView CreateVerticalSplitView(VisualElement up, VisualElement down)
    {
        return new SplitView(up, down, MouseCursor.ResizeVertical);
    }

    private SplitView(VisualElement left, VisualElement flexed, MouseCursor cursor = MouseCursor.ResizeHorizontal)
    {
        FixedPanel = left;
        mFlexedPanel = flexed;
        CursorDirection = cursor;
        InitLayout();
        InitStyles(cursor);
    }

    private void InitLayout()
    {
        var dragLine = new Box();
        mDragLine = new VisualElement();
        mDragLine.AddManipulator(new SquareResizer(this));
        dragLine.Add(mDragLine);

        Add(FixedPanel);
        Add(dragLine);
        Add(mFlexedPanel);

    }

    private void InitStyles(MouseCursor cursor)
    {
        style.flexGrow = 1;
        mFlexedPanel.style.flexGrow = 1;
        switch (cursor)
        {
            case MouseCursor.ResizeHorizontal:
                style.flexDirection = FlexDirection.Row;
                mDragLine.style.width = 2;
                break;
            case MouseCursor.ResizeVertical:
                style.flexDirection = FlexDirection.Column;
                mDragLine.style.height = 2;
                break;
        }

        mDragLine.style.flexGrow = 1;
        mDragLine.style.backgroundColor = new StyleColor(Color.black);
        mDragLine.style.cursor = LoadCursor(cursor);
    }

    private StyleCursor LoadCursor(MouseCursor mouseCursor)
    {
        var cursorType = typeof(UnityEngine.UIElements.Cursor);
        var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var defaultCursorIdProperty = cursorType.GetProperty("defaultCursorId", bindingFlags);
        object boxed = new UnityEngine.UIElements.Cursor();
        defaultCursorIdProperty.SetValue(boxed, (int)mouseCursor, null);
        return (UnityEngine.UIElements.Cursor)boxed;
    }

    class SquareResizer : MouseManipulator
    {
        private Vector2 mStart;
        private bool mActive;
        private SplitView mSplitter;
        public SquareResizer(SplitView splitter)
        {
            mSplitter = splitter;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            mActive = false;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!mActive || !target.HasMouseCapture() || !CanStopManipulation(evt))
                return;
            mActive = false;
            target.ReleaseMouse();
            evt.StopPropagation();

        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!mActive || !target.HasMouseCapture()) return;

            var diff = evt.localMousePosition - mStart;
            switch (mSplitter.CursorDirection)
            {
                case MouseCursor.ResizeHorizontal:
                    var width = mSplitter.FixedPanel.layout.width + diff.x;
                    width = Mathf.Max(mSplitter.FixedPanel.resolvedStyle.minWidth.value, width);
                    mSplitter.FixedPanel.style.width = width;
                    break;
                case MouseCursor.ResizeVertical:
                    var height = mSplitter.FixedPanel.layout.height + diff.y;
                    height = Mathf.Max(mSplitter.FixedPanel.resolvedStyle.minHeight.value, height);
                    mSplitter.FixedPanel.style.height = height;
                    break;
            }

            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (mActive)
            {
                evt.StopImmediatePropagation();
                return;
            }

            if (CanStartManipulation(evt))
            {
                mStart = evt.localMousePosition;

                mActive = true;
                target.CaptureMouse();
                evt.StopPropagation();
            }
        }


    }
}
