using System;
using System.Collections.Generic;
using System.Linq;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.ui;

namespace Unity.UIWidgets.gestures {
    public class Velocity : IEquatable<Velocity> {
        public Velocity(
            Offset pixelsPerSecond = null
        ) {
            this.pixelsPerSecond = pixelsPerSecond ?? Offset.zero;
        }

        public static readonly Velocity zero = new Velocity();

        public readonly Offset pixelsPerSecond;

        public static Velocity operator -(Velocity a) {
            return new Velocity(pixelsPerSecond: -a.pixelsPerSecond);
        }

        public static Velocity operator -(Velocity a, Velocity b) {
            return new Velocity(
                pixelsPerSecond: a.pixelsPerSecond - b.pixelsPerSecond);
        }

        public static Velocity operator +(Velocity a, Velocity b) {
            return new Velocity(
                pixelsPerSecond: a.pixelsPerSecond + b.pixelsPerSecond);
        }

        public Velocity clampMagnitude(double minValue, double maxValue) {
            D.assert(minValue >= 0.0);
            D.assert(maxValue >= 0.0 && maxValue >= minValue);
            double valueSquared = this.pixelsPerSecond.distanceSquared;
            if (valueSquared > maxValue * maxValue) {
                return new Velocity(pixelsPerSecond: (this.pixelsPerSecond / this.pixelsPerSecond.distance) * maxValue);
            }

            if (valueSquared < minValue * minValue) {
                return new Velocity(pixelsPerSecond: (this.pixelsPerSecond / this.pixelsPerSecond.distance) * minValue);
            }

            return this;
        }

        public bool Equals(Velocity other) {
            if (object.ReferenceEquals(null, other)) return false;
            if (object.ReferenceEquals(this, other)) return true;
            return object.Equals(this.pixelsPerSecond, other.pixelsPerSecond);
        }

        public override bool Equals(object obj) {
            if (object.ReferenceEquals(null, obj)) return false;
            if (object.ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return this.Equals((Velocity) obj);
        }

        public override int GetHashCode() {
            return (this.pixelsPerSecond != null ? this.pixelsPerSecond.GetHashCode() : 0);
        }

        public static bool operator ==(Velocity left, Velocity right) {
            return object.Equals(left, right);
        }

        public static bool operator !=(Velocity left, Velocity right) {
            return !object.Equals(left, right);
        }

        public override string ToString() {
            return string.Format("Velocity({0:F1}, {1:F1})", this.pixelsPerSecond.dx, this.pixelsPerSecond.dy);
        }
    }

    public class VelocityEstimate {
        public VelocityEstimate(
            Offset pixelsPerSecond,
            double confidence,
            TimeSpan duration,
            Offset offset
        ) {
            D.assert(pixelsPerSecond != null);
            D.assert(offset != null);
            this.pixelsPerSecond = pixelsPerSecond;
            this.confidence = confidence;
            this.duration = duration;
            this.offset = offset;
        }

        public readonly Offset pixelsPerSecond;

        public readonly double confidence;

        public readonly TimeSpan duration;

        public readonly Offset offset;

        public override string ToString() {
            return string.Format("VelocityEstimate({0:F1}, {1:F1}; offset: {2}, duration: {3}, confidence: {4:F1})",
                this.pixelsPerSecond.dx, this.pixelsPerSecond.dy, this.offset, this.duration, this.confidence);
        }
    }

    class _PointAtTime {
        internal _PointAtTime(Offset point, DateTime time) {
            D.assert(point != null);
            this.point = point;
            this.time = time;
        }

        public readonly Offset point;

        public readonly DateTime time;

        public override string ToString() {
            return string.Format("_PointAtTime({0} at {1})", this.point, this.time);
        }
    }

    public class VelocityTracker {
        const int _assumePointerMoveStoppedMilliseconds = 40;
        const int _historySize = 20;
        const int _horizonMilliseconds = 100;
        const int _minSampleSize = 3;

        readonly List<_PointAtTime> _samples = Enumerable.Repeat<_PointAtTime>(null, _historySize).ToList();
        int _index = 0;

        public void addPosition(DateTime time, Offset position) {
            this._index += 1;
            if (this._index == _historySize) {
                this._index = 0;
            }

            this._samples[this._index] = new _PointAtTime(position, time);
        }

        public VelocityEstimate getVelocityEstimate() {
            List<double> x = new List<double>();
            List<double> y = new List<double>();
            List<double> w = new List<double>();
            List<double> time = new List<double>();
            int sampleCount = 0;
            int index = this._index;

            _PointAtTime newestSample = this._samples[index];
            if (newestSample == null) {
                return null;
            }

            _PointAtTime previousSample = newestSample;
            _PointAtTime oldestSample = newestSample;

            do {
                _PointAtTime sample = this._samples[index];
                if (sample == null)
                    break;

                double age = (newestSample.time - sample.time).TotalMilliseconds;
                double delta = Math.Abs((sample.time - previousSample.time).TotalMilliseconds);
                previousSample = sample;
                if (age > _horizonMilliseconds || delta > _assumePointerMoveStoppedMilliseconds) {
                    break;
                }

                oldestSample = sample;
                Offset position = sample.point;
                x.Add(position.dx);
                y.Add(position.dy);
                w.Add(1.0);
                time.Add(-age);
                index = (index == 0 ? _historySize : index) - 1;

                sampleCount += 1;
            } while (sampleCount < _historySize);

            if (sampleCount >= _minSampleSize) {
                LeastSquaresSolver xSolver = new LeastSquaresSolver(time, x, w);
                PolynomialFit xFit = xSolver.solve(2);
                if (xFit != null) {
                    LeastSquaresSolver ySolver = new LeastSquaresSolver(time, y, w);
                    PolynomialFit yFit = ySolver.solve(2);
                    if (yFit != null) {
                        return new VelocityEstimate(
                            pixelsPerSecond: new Offset(xFit.coefficients[1] * 1000, yFit.coefficients[1] * 1000),
                            confidence: xFit.confidence * yFit.confidence,
                            duration: newestSample.time - oldestSample.time,
                            offset: newestSample.point - oldestSample.point
                        );
                    }
                }
            }

            return new VelocityEstimate(
                pixelsPerSecond: Offset.zero,
                confidence: 1.0,
                duration: newestSample.time - oldestSample.time,
                offset: newestSample.point - oldestSample.point
            );
        }

        public Velocity getVelocity() {
            VelocityEstimate estimate = this.getVelocityEstimate();
            if (estimate == null || estimate.pixelsPerSecond == Offset.zero) {
                return Velocity.zero;
            }

            return new Velocity(pixelsPerSecond: estimate.pixelsPerSecond);
        }
    }
}