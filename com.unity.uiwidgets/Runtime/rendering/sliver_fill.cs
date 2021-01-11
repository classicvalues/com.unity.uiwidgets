using System;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.painting;
using Unity.UIWidgets.ui;
using UnityEngine;

namespace Unity.UIWidgets.rendering {
    
    class RenderSliverFillRemainingWithScrollable : RenderSliverSingleBoxAdapter {
        public RenderSliverFillRemainingWithScrollable(RenderBox child = null) : base(child: child) {
            
        }

        protected override void performLayout() { 
            SliverConstraints constraints = this.constraints;
            float extent = constraints.remainingPaintExtent - Mathf.Min(constraints.overlap, 0.0f);

            if (child != null) 
                child.layout(constraints.asBoxConstraints(
                minExtent: extent,
                maxExtent: extent
              ));

            float paintedChildSize = calculatePaintOffset(constraints, from: 0.0f, to: extent);
            D.assert(paintedChildSize.isFinite());
            D.assert(paintedChildSize >= 0.0);
            geometry = new SliverGeometry(
              scrollExtent: constraints.viewportMainAxisExtent,
              paintExtent: paintedChildSize,
              maxPaintExtent: paintedChildSize,
              hasVisualOverflow: extent > constraints.remainingPaintExtent || constraints.scrollOffset > 0.0
            );
            if (child != null)
              setChildParentData(child, constraints, geometry);
        }
    }
    public class RenderSliverFillRemaining : RenderSliverSingleBoxAdapter {
        public RenderSliverFillRemaining( RenderBox child  = null) : base(child: child) { }
        protected override void performLayout() { 
            SliverConstraints constraints = this.constraints;
            float extent = constraints.viewportMainAxisExtent - constraints.precedingScrollExtent;
            if (child != null) { 
                float childExtent = 0f; 
                switch (constraints.axis) { 
                    case Axis.horizontal: 
                        childExtent = child.getMaxIntrinsicWidth(constraints.crossAxisExtent); 
                        break; 
                    case Axis.vertical: 
                        childExtent = child.getMaxIntrinsicHeight(constraints.crossAxisExtent); 
                        break;
                }
                extent = Mathf.Max(extent, childExtent); 
                child.layout(constraints.asBoxConstraints(
                    minExtent: extent, 
                    maxExtent: extent
                    )); 
            }
            
            D.assert(extent.isFinite(),()=> 
                "The calculated extent for the child of SliverFillRemaining is not finite. "+
                "This can happen if the child is a scrollable, in which case, the " +
                "hasScrollBody property of SliverFillRemaining should not be set to " +
                "false."
            ); 
            float paintedChildSize = calculatePaintOffset(constraints, from: 0.0f, to: extent); 
            D.assert(paintedChildSize.isFinite()); 
            D.assert(paintedChildSize >= 0.0); 
            geometry = new SliverGeometry(
                scrollExtent: extent, 
                paintExtent: paintedChildSize, 
                maxPaintExtent: paintedChildSize, 
                hasVisualOverflow: extent > constraints.remainingPaintExtent || constraints.scrollOffset > 0.0
                ); 
            if (child != null) 
                setChildParentData(child, constraints, geometry); 
        } 
    }

    public class RenderSliverFillViewport : RenderSliverFixedExtentBoxAdaptor {
        public RenderSliverFillViewport(
            RenderSliverBoxChildManager childManager = null,
            float viewportFraction = 1.0f
        ) :
            base(childManager: childManager) {
            D.assert(viewportFraction > 0.0);
            _viewportFraction = viewportFraction;
        }

        public override float itemExtent {
            get { return constraints.viewportMainAxisExtent * viewportFraction; }
            set { }
        }

        float _viewportFraction;

        public float viewportFraction {
            get { return _viewportFraction; }
            set {
                if (_viewportFraction == value) {
                    return;
                }

                _viewportFraction = value;
                markNeedsLayout();
            }
        }


        float _padding {
            get { return (1.0f - viewportFraction) * constraints.viewportMainAxisExtent * 0.5f; }
        }

        protected override float indexToLayoutOffset(float itemExtent, int index) {
            return _padding + base.indexToLayoutOffset(itemExtent, index);
        }

        protected override int getMinChildIndexForScrollOffset(float scrollOffset, float itemExtent) {
            return base.getMinChildIndexForScrollOffset(Mathf.Max(scrollOffset - _padding, 0.0f), itemExtent);
        }

        protected override int getMaxChildIndexForScrollOffset(float scrollOffset, float itemExtent) {
            return base.getMaxChildIndexForScrollOffset(Mathf.Max(scrollOffset - _padding, 0.0f), itemExtent);
        }

        protected override float estimateMaxScrollOffset(SliverConstraints constraints,
            int firstIndex = 0,
            int lastIndex = 0,
            float leadingScrollOffset = 0.0f,
            float trailingScrollOffset = 0.0f
        ) {
            float padding = _padding;
            return childManager.estimateMaxScrollOffset(
                       constraints,
                       firstIndex: firstIndex,
                       lastIndex: lastIndex,
                       leadingScrollOffset: leadingScrollOffset - padding,
                       trailingScrollOffset: trailingScrollOffset - padding
                   ) + padding + padding;
        }
    }
    
    public class RenderSliverFillRemainingAndOverscroll : RenderSliverSingleBoxAdapter {
   
        public RenderSliverFillRemainingAndOverscroll(RenderBox child = null) : base(child: child) {
        }

        protected override void performLayout() { 
            SliverConstraints constraints = this.constraints;
            float extent = constraints.viewportMainAxisExtent - constraints.precedingScrollExtent;
            float maxExtent = constraints.remainingPaintExtent - Mathf.Min(constraints.overlap, 0.0f);
            if (child != null) { 
                float childExtent = 0f; 
                switch (constraints.axis) { 
                    case Axis.horizontal: 
                        childExtent = child.getMaxIntrinsicWidth(constraints.crossAxisExtent); 
                        break; 
                    case Axis.vertical: 
                        childExtent = child.getMaxIntrinsicHeight(constraints.crossAxisExtent); 
                        break; 
                }
                extent = Mathf.Max(extent, childExtent);
                maxExtent = Mathf.Max(extent, maxExtent);
                child.layout(constraints.asBoxConstraints(minExtent: extent, maxExtent: maxExtent)); 
            }
            D.assert(extent.isFinite(),()=> 
                "The calculated extent for the child of SliverFillRemaining is not finite. " +
            "This can happen if the child is a scrollable, in which case, the " +
            "hasScrollBody property of SliverFillRemaining should not be set to " +
            "false."
                ); 
            float paintedChildSize = calculatePaintOffset(constraints, from: 0.0f, to: extent);
            D.assert(paintedChildSize.isFinite());
            D.assert(paintedChildSize >= 0.0f);
            geometry = new SliverGeometry(
              scrollExtent: extent,
              paintExtent: Mathf.Min(maxExtent, constraints.remainingPaintExtent),
              maxPaintExtent: maxExtent ,
              hasVisualOverflow: extent > constraints.remainingPaintExtent || constraints.scrollOffset > 0.0
            );
            if (child != null)
                setChildParentData(child, constraints, geometry);
        }
    }


}