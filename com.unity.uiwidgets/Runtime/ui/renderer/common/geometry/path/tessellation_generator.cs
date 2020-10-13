using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.UIWidgets.uiOld{
    class uiTessellationKey : PoolObject, IEquatable<uiTessellationKey> {
        public float x2;
        public float y2;
        public float x3;
        public float y3;
        public float x4;
        public float y4;
        public float tessTol;

        public static uiTessellationKey create(float x1, float y1, float x2, float y2, float x3, float y3, float x4,
            float y4,
            float tessTol) {
            var newKey = ObjectPool<uiTessellationKey>.alloc();
            newKey.x2 = x2 - x1;
            newKey.y2 = y2 - y1;
            newKey.x3 = x3 - x1;
            newKey.y3 = y3 - y1;
            newKey.x4 = x4 - x1;
            newKey.y4 = y4 - y1;
            newKey.tessTol = tessTol;

            return newKey;
        }

        public uiTessellationKey() {
        }

        public bool Equals(uiTessellationKey other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return x2 == other.x2 && y2 == other.y2 && x3 == other.x3 &&
                   y3 == other.y3 && x4 == other.x4 && y4 == other.y4 &&
                   tessTol == other.tessTol;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj.GetType() != GetType()) {
                return false;
            }

            return Equals((uiTessellationKey) obj);
        }

        public override unsafe int GetHashCode() {
            unchecked {
                var hashCode = 0;
                float x = x2;
                hashCode ^= *(int*) &x;
                x = y2;
                hashCode = (hashCode * 13) ^ *(int*) &x;
                x = x3;
                hashCode = (hashCode * 13) ^ *(int*) &x;
                x = y3;
                hashCode = (hashCode * 13) ^ *(int*) &x;
                x = x4;
                hashCode = (hashCode * 13) ^ *(int*) &x;
                x = y4;
                hashCode = (hashCode * 13) ^ *(int*) &x;
                x = tessTol;
                hashCode = (hashCode * 13) ^ *(int*) &x;
                return hashCode;
            }
        }

        public static bool operator ==(uiTessellationKey left, uiTessellationKey right) {
            return Equals(left, right);
        }

        public static bool operator !=(uiTessellationKey left, uiTessellationKey right) {
            return !Equals(left, right);
        }

        public override string ToString() {
            return $"uiTessellationKey(" +
                   $"x2: {x2}, " +
                   $"y2: {y2}, " +
                   $"x3: {x3}, " +
                   $"y3: {y3}, " +
                   $"x4: {x4}, " +
                   $"y4: {y4}, " +
                   $"tessTol: {tessTol})";
        }
    }

    class uiTessellationInfo : PoolObject {
        public uiTessellationKey key;
        public uiList<Vector2> points;
        long _timeToLive;

        public static uiTessellationInfo create(uiTessellationKey key, uiList<Vector2> points, int timeToLive = 5) {
            var newInfo = ObjectPool<uiTessellationInfo>.alloc();
            newInfo.points = points;
            newInfo.key = key;
            newInfo.touch(timeToLive);

            return newInfo;
        }

        public uiTessellationInfo() {
        }

        public override void clear() {
            ObjectPool<uiList<Vector2>>.release(points);
        }

        public long timeToLive {
            get { return _timeToLive; }
        }

        public void touch(long timeTolive = 5) {
            _timeToLive = timeTolive + TextBlobMesh.frameCount;
        }
    }


    static class uiTessellationGenerator {
        static readonly Dictionary<uiTessellationKey, uiTessellationInfo> _tessellations =
            new Dictionary<uiTessellationKey, uiTessellationInfo>();

        static long _frameCount = 0;

        public static long frameCount {
            get { return _frameCount; }
        }

        public static int tessellationCount {
            get { return _tessellations.Count; }
        }

        public static void tickNextFrame() {
            _frameCount++;

            var keysToRemove = _tessellations.Values.Where(info => info.timeToLive < _frameCount)
                .Select(info => info.key).ToList();
            foreach (var key in keysToRemove) {
                ObjectPool<uiTessellationKey>.release(key);
                ObjectPool<uiTessellationInfo>.release(_tessellations[key]);
                _tessellations.Remove(key);
            }
        }

        public static uiList<Vector2> tessellateBezier(float x1, float y1, float x2, float y2,
            float x3, float y3, float x4, float y4, float tessTol) {
            var key = uiTessellationKey.create(x1, y1, x2, y2, x3, y3, x4, y4, tessTol);

            _tessellations.TryGetValue(key, out var uiTessellationInfo);
            if (uiTessellationInfo != null) {
                ObjectPool<uiTessellationKey>.release(key);
                uiTessellationInfo.touch();
                return uiTessellationInfo.points;
            }

            var points = _tessellateBezier(x1, y1, x2, y2, x3, y3, x4, y4, tessTol);
            _tessellations[key] = uiTessellationInfo.create(key, points);

            return points;
        }

        struct _StackData {
            public float x1;
            public float y1;
            public float x2;
            public float y2;
            public float x3;
            public float y3;
            public float x4;
            public float y4;
            public int level;
        }

        static readonly Stack<_StackData> _stack = new Stack<_StackData>();

        static uiList<Vector2> _tessellateBezier(
            float x1, float y1, float x2, float y2,
            float x3, float y3, float x4, float y4,
            float tessTol) {
            x2 = x2 - x1;
            y2 = y2 - y1;
            x3 = x3 - x1;
            y3 = y3 - y1;
            x4 = x4 - x1;
            y4 = y4 - y1;

            var points = ObjectPool<uiList<Vector2>>.alloc();

            _stack.Clear();
            _stack.Push(new _StackData {
                x1 = 0, y1 = 0, x2 = x2, y2 = y2, x3 = x3, y3 = y3, x4 = x4, y4 = y4, level = 0,
            });

            while (_stack.Count > 0) {
                var stackData = _stack.Pop();
                x1 = stackData.x1;
                y1 = stackData.y1;
                x2 = stackData.x2;
                y2 = stackData.y2;
                x3 = stackData.x3;
                y3 = stackData.y3;
                x4 = stackData.x4;
                y4 = stackData.y4;
                int level = stackData.level;

                float dx = x4 - x1;
                float dy = y4 - y1;
                float d2 = Mathf.Abs((x2 - x4) * dy - (y2 - y4) * dx);
                float d3 = Mathf.Abs((x3 - x4) * dy - (y3 - y4) * dx);

                if ((d2 + d3) * (d2 + d3) <= tessTol * (dx * dx + dy * dy)) {
                    points.Add(new Vector2(x4, y4));
                    continue;
                }

                float x12 = (x1 + x2) * 0.5f;
                float y12 = (y1 + y2) * 0.5f;
                float x23 = (x2 + x3) * 0.5f;
                float y23 = (y2 + y3) * 0.5f;
                float x34 = (x3 + x4) * 0.5f;
                float y34 = (y3 + y4) * 0.5f;
                float x123 = (x12 + x23) * 0.5f;
                float y123 = (y12 + y23) * 0.5f;
                float x234 = (x23 + x34) * 0.5f;
                float y234 = (y23 + y34) * 0.5f;
                float x1234 = (x123 + x234) * 0.5f;
                float y1234 = (y123 + y234) * 0.5f;

                if (level < 10) {
                    _stack.Push(new _StackData {
                        x1 = x1234, y1 = y1234, x2 = x234, y2 = y234, x3 = x34, y3 = y34, x4 = x4, y4 = y4,
                        level = level + 1,
                    });
                    _stack.Push(new _StackData {
                        x1 = x1, y1 = y1, x2 = x12, y2 = y12, x3 = x123, y3 = y123, x4 = x1234, y4 = y1234,
                        level = level + 1,
                    });
                }
            }

            return points;
        }
    }
}