﻿using System;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.gestures;
using Unity.UIWidgets.painting;
using Unity.UIWidgets.ui;

namespace Unity.UIWidgets.rendering {
    public class RendererBinding : PaintingBinding {
        public new static RendererBinding instance {
            get { return (RendererBinding) PaintingBinding.instance; }
            set { PaintingBinding.instance = value; }
        }

        public RendererBinding(bool inEditorWindow = false) {
            _pipelineOwner = new PipelineOwner(
                onNeedVisualUpdate: ensureVisualUpdate
            );

            Window.instance.onMetricsChanged += handleMetricsChanged;
            Window.instance.onTextScaleFactorChanged += handleTextScaleFactorChanged;
            Window.instance.onPlatformBrightnessChanged += handlePlatformBrightnessChanged;
            initRenderView();
            D.assert(renderView != null);
            addPersistentFrameCallback(_handlePersistentFrameCallback);

            this.inEditorWindow = inEditorWindow;
            _mouseTracker = _createMouseTracker();
        }

        public void initRenderView() {
            D.assert(renderView == null);
            renderView = new RenderView(configuration: createViewConfiguration());
            renderView.scheduleInitialFrame();
        }

        public MouseTracker mouseTracker {
            get { return _mouseTracker; }
        }

        MouseTracker _mouseTracker;

        public PipelineOwner pipelineOwner {
            get { return _pipelineOwner; }
        }

        readonly PipelineOwner _pipelineOwner;

        public RenderView renderView {
            get { return (RenderView) _pipelineOwner.rootNode; }
            set { _pipelineOwner.rootNode = value; }
        }

        protected virtual void handleMetricsChanged() {
            renderView.configuration = createViewConfiguration();
            scheduleForcedFrame();
        }

        protected virtual void handleTextScaleFactorChanged() {
        }

        protected virtual void handlePlatformBrightnessChanged() {
        }

        protected virtual ViewConfiguration createViewConfiguration() {
            var devicePixelRatio = Window.instance.devicePixelRatio;
            return new ViewConfiguration(
                size: Window.instance.physicalSize / devicePixelRatio,
                devicePixelRatio: devicePixelRatio
            );
        }

        void _handlePersistentFrameCallback(TimeSpan timeStamp) {
            drawFrame();
        }

        readonly protected bool inEditorWindow;

        MouseTracker _createMouseTracker() {
            return new MouseTracker(pointerRouter, (Offset offset) => {
                return renderView.layer.find<MouseTrackerAnnotation>(
                    offset
                );
            }, inEditorWindow);
        }

        protected virtual void drawFrame() {
            pipelineOwner.flushLayout();
            pipelineOwner.flushCompositingBits();
            pipelineOwner.flushPaint();
            renderView.compositeFrame();
        }

        public override void hitTest(HitTestResult result, Offset position) {
            D.assert(renderView != null);
            renderView.hitTest(result, position: position);
            base.hitTest(result, position);
        }
    }
}