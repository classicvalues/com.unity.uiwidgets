using System.Collections.Generic;
using System.Linq;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.gestures;
using Unity.UIWidgets.ui;

namespace Unity.UIWidgets.rendering {
    public class MultiChildLayoutParentData : ContainerParentDataMixinBoxParentData<RenderBox> {
        public object id;

        public override string ToString() {
            return $"{base.ToString()}; id={id}";
        }
    }

    public abstract class MultiChildLayoutDelegate {
        Dictionary<object, RenderBox> _idToChild;
        HashSet<RenderBox> _debugChildrenNeedingLayout;

        public bool hasChild(object childId) {
            return _idToChild.getOrDefault(childId) != null;
        }

        public Size layoutChild(object childId, BoxConstraints constraints) {
            RenderBox child = _idToChild[childId];
            D.assert(() => {
                if (child == null) {
                    throw new UIWidgetsError(
                        $"The {this} custom multichild layout delegate tried to lay out a non-existent child.\n" +
                        $"There is no child with the id \"{childId}\"."
                    );
                }

                if (!_debugChildrenNeedingLayout.Remove(child)) {
                    throw new UIWidgetsError(
                        $"The $this custom multichild layout delegate tried to lay out the child with id \"{childId}\" more than once.\n" +
                        "Each child must be laid out exactly once."
                    );
                }

                try {
                    D.assert(constraints.debugAssertIsValid(isAppliedConstraint: true));
                }
                catch (AssertionError exception) {
                    throw new UIWidgetsError(
                        $"The {this} custom multichild layout delegate provided invalid box constraints for the child with id \"{childId}\".\n" +
                        $"{exception}n" +
                        "The minimum width and height must be greater than or equal to zero.\n" +
                        "The maximum width must be greater than or equal to the minimum width.\n" +
                        "The maximum height must be greater than or equal to the minimum height.");
                }

                return true;
            });
            child.layout(constraints, parentUsesSize: true);
            return child.size;
        }

        public void positionChild(object childId, Offset offset) {
            RenderBox child = _idToChild[childId];
            D.assert(() => {
                if (child == null) {
                    throw new UIWidgetsError(
                        $"The {this} custom multichild layout delegate tried to position out a non-existent child:\n" +
                        $"There is no child with the id \"{childId}\"."
                    );
                }

                if (offset == null) {
                    throw new UIWidgetsError(
                        $"The {this} custom multichild layout delegate provided a null position for the child with id \"{childId}\"."
                    );
                }

                return true;
            });
            MultiChildLayoutParentData childParentData = (MultiChildLayoutParentData) child.parentData;
            childParentData.offset = offset;
        }

        string _debugDescribeChild(RenderBox child) {
            MultiChildLayoutParentData childParentData = (MultiChildLayoutParentData) child.parentData;
            return $"{childParentData.id}: {child}";
        }


        internal void _callPerformLayout(Size size, RenderBox firstChild) {
            Dictionary<object, RenderBox> previousIdToChild = _idToChild;

            HashSet<RenderBox> debugPreviousChildrenNeedingLayout = null;
            D.assert(() => {
                debugPreviousChildrenNeedingLayout = _debugChildrenNeedingLayout;
                _debugChildrenNeedingLayout = new HashSet<RenderBox>();
                return true;
            });

            try {
                _idToChild = new Dictionary<object, RenderBox>();
                RenderBox child = firstChild;
                while (child != null) {
                    MultiChildLayoutParentData childParentData = (MultiChildLayoutParentData) child.parentData;
                    D.assert(() => {
                        if (childParentData.id == null) {
                            throw new UIWidgetsError(
                                "The following child has no ID:\n" +
                                $"  {child}\n" +
                                "Every child of a RenderCustomMultiChildLayoutBox must have an ID in its parent data."
                            );
                        }

                        return true;
                    });
                    _idToChild[childParentData.id] = child;
                    D.assert(() => {
                        _debugChildrenNeedingLayout.Add(child);
                        return true;
                    });
                    child = childParentData.nextSibling;
                }

                performLayout(size);
                D.assert(() => {
                    if (_debugChildrenNeedingLayout.isNotEmpty()) {
                        if (_debugChildrenNeedingLayout.Count > 1) {
                            throw new UIWidgetsError(
                                $"The $this custom multichild layout delegate forgot to lay out the following children:\n" +
                                $"  {string.Join("\n  ", _debugChildrenNeedingLayout.Select(_debugDescribeChild))}\n" +
                                "Each child must be laid out exactly once."
                            );
                        }
                        else {
                            throw new UIWidgetsError(
                                $"The $this custom multichild layout delegate forgot to lay out the following child:\n" +
                                $"  {_debugDescribeChild(_debugChildrenNeedingLayout.First())}\n" +
                                "Each child must be laid out exactly once."
                            );
                        }
                    }

                    return true;
                });
            }
            finally {
                _idToChild = previousIdToChild;
                D.assert(() => {
                    _debugChildrenNeedingLayout = debugPreviousChildrenNeedingLayout;
                    return true;
                });
            }
        }

        public virtual Size getSize(BoxConstraints constraints) {
            return constraints.biggest;
        }


        public abstract void performLayout(Size size);


        public abstract bool shouldRelayout(MultiChildLayoutDelegate oldDelegate);

        public override string ToString() {
            return $"{GetType()}";
        }
    }

    public class RenderCustomMultiChildLayoutBox : RenderBoxContainerDefaultsMixinContainerRenderObjectMixinRenderBox<
        RenderBox
        , MultiChildLayoutParentData> {
        public RenderCustomMultiChildLayoutBox(
            List<RenderBox> children = null,
            MultiChildLayoutDelegate layoutDelegate = null
        ) {
            D.assert(layoutDelegate != null);
            _delegate = layoutDelegate;
            addAll(children);
        }

        public override void setupParentData(RenderObject child) {
            if (!(child.parentData is MultiChildLayoutParentData)) {
                child.parentData = new MultiChildLayoutParentData();
            }
        }

        public MultiChildLayoutDelegate layoutDelegate {
            get { return _delegate; }
            set {
                D.assert(value != null);
                if (_delegate == value) {
                    return;
                }

                if (value.GetType() != _delegate.GetType() || value.shouldRelayout(_delegate)) {
                    markNeedsLayout();
                }

                _delegate = value;
            }
        }

        MultiChildLayoutDelegate _delegate;


        Size _getSize(BoxConstraints constraints) {
            D.assert(constraints.debugAssertIsValid());
            return constraints.constrain(_delegate.getSize(constraints));
        }

        protected override float computeMinIntrinsicWidth(float height) {
            float width = _getSize(BoxConstraints.tightForFinite(height: height)).width;
            if (width.isFinite()) {
                return width;
            }

            return 0.0f;
        }

        protected override float computeMaxIntrinsicWidth(float height) {
            float width = _getSize(BoxConstraints.tightForFinite(height: height)).width;
            if (width.isFinite()) {
                return width;
            }

            return 0.0f;
        }

        protected override float computeMinIntrinsicHeight(float width) {
            float height = _getSize(BoxConstraints.tightForFinite(width: width)).height;
            if (height.isFinite()) {
                return height;
            }

            return 0.0f;
        }

        protected internal override float computeMaxIntrinsicHeight(float width) {
            float height = _getSize(BoxConstraints.tightForFinite(width: width)).height;
            if (height.isFinite()) {
                return height;
            }

            return 0.0f;
        }

        protected override void performLayout() {
            size = _getSize(constraints);
            layoutDelegate._callPerformLayout(size, firstChild);
        }

        public override void paint(PaintingContext context, Offset offset) {
            defaultPaint(context, offset);
        }

        protected override bool hitTestChildren(BoxHitTestResult result, Offset position) {
            return defaultHitTestChildren(result, position: position);
        }
    }
}