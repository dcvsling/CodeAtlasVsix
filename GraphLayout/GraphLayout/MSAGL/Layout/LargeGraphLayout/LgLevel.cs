using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Msagl.Core.DataStructures;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.GraphAlgorithms;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.DebugHelpers;
using SymmetricSegment = Microsoft.Msagl.Core.DataStructures.SymmetricTuple<Microsoft.Msagl.Core.Geometry.Point>;
namespace Microsoft.Msagl.Layout.LargeGraphLayout {
    /// <summary>
    /// class keeping a level info
    /// </summary>
    public class LgLevel {
        internal readonly Dictionary<SymmetricSegment, Rail> _railDictionary =
            new Dictionary<SymmetricSegment, Rail>();

        internal Dictionary<SymmetricSegment, Rail> RailDictionary {
            get { return _railDictionary; }
        }

        internal readonly Dictionary<Edge, Set<Rail>> _railsOfEdges = new Dictionary<Edge, Set<Rail>>();
        internal Set<Rail> HighlightedRails = new Set<Rail>();
        internal readonly int ZoomLevel;
        readonly GeometryGraph _geomGraph;
        internal RTree<Rail> _railTree = new RTree<Rail>();
        internal RTree<Rail> RailTree {
            get { return _railTree; }
        }

        RTree<LgNodeInfo> _nodeInfoTree = new RTree<LgNodeInfo>();

        internal readonly Dictionary<Edge, List<Rail>> _orderedRailsOfEdges = new Dictionary<Edge, List<Rail>>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="zoomLevel"></param>
        /// <param name="geomGraph">needed only for statistics</param>
        internal LgLevel(int zoomLevel, GeometryGraph geomGraph) {
            _geomGraph = geomGraph;
            ZoomLevel = zoomLevel;
        }

        internal RTree<LgNodeInfo> NodeInfoTree {
            get { return _nodeInfoTree; }            
        }


        internal void CreateEmptyRailTree() {
            _railTree = new RTree<Rail>();
        }

        //public void CreateRailTree() {
        //    RailTree = new RTree<Rail>(
        //        RailDictionary.Values.Select(rail => new KeyValuePair<Rectangle, Rail>(rail.BoundingBox, rail)));
        //}

        /// <summary>
        /// get endpoint tuples of all rails
        /// </summary>
        /// <returns></returns>
        public List<SymmetricSegment> GetAllRailsEndpoints() {
            var endpts = new List<SymmetricSegment>();
            Point p0, p1;
            foreach (var rail in _railTree.GetAllLeaves()) {
                if (rail.GetStartEnd(out p0, out p1)) {
                    endpts.Add(new SymmetricSegment(p0, p1));
                }
            }
            return endpts;
        }

        public List<Rail> FetchOrCreateRailSequence(List<Point> path) {
            List<Rail> rails = new List<Rail>();
            for (int i = 0; i < path.Count - 1; i++) {
                var rail = FindOrCreateRail(path[i], path[i + 1]);
                if (rail == null)
                    continue;

                rails.Add(rail);
            }
            return rails;
        }

        public Rail FindOrCreateRail(Point s, Point t) {
            var p0 = s;
            var p1 = t;

            var t1 = new SymmetricSegment(p0, p1);
            Rail rail;
            if (RailDictionary.TryGetValue(t1, out rail)) return rail;
            var t2 = new SymmetricSegment(p1, p0);
            if (RailDictionary.TryGetValue(t2, out rail)) return rail;

            // no rail exists // roman: please check that this code really can be commented out and does need to be fixed instead 
            /*
             * var q0 = VisGraph.GetPointOfVisGraphVertex(s);
            var q1 = VisGraph.GetPointOfVisGraphVertex(t);
            if (q0 == null || q1 == null)
            {
                //no visgraph vertex found
            }
            else
            {
                var edge = VisGraph.FindEdge(q0.Point, q1.Point);
            }*/
            return CreateRail(p0, p1);
        }

        public Rail CreateRail(Point ep0, Point ep1) {
            var st = new SymmetricSegment(ep0, ep1);
            Rail rail;
            if (RailDictionary.TryGetValue(st, out rail)) {
                return rail;
            }
            var ls = new LineSegment(ep0, ep1);
            rail = new Rail(ls, ZoomLevel);
            RailTree.Add(rail.BoundingBox, rail);
            RailDictionary.Add(st, rail);
            return rail;
        }

        public Rail FindRail(Point s, Point t) {
            var p0 = s;
            var p1 = t;
            var ss = new SymmetricSegment(p1, p0);
            Rail rail;
            RailDictionary.TryGetValue(ss, out rail);
            return rail;
        }
        
        internal IEnumerable<Rail> GetRailsIntersectingRect(Rectangle visibleRectange) {
            var ret = new Set<Rail>();
            foreach (var rail in _railTree.GetAllIntersecting(visibleRectange))
                ret.Insert(rail);
            return ret;
        }

        internal List<Edge> GetEdgesPassingThroughRail(Rail rail) {
            return (from kv in _railsOfEdges where kv.Value.Contains(rail) select kv.Key).ToList();
        }

        public Rail RemoveRailFromRtree(Rail rail) {
            return _railTree.Remove(rail.BoundingBox, rail);
        }

        /// <summary>
        /// try adding single rail to dictionary
        /// </summary>
        /// <param name="rail"></param>
        /// <returns>true iff the rail does not belong to _railDictionary</returns>
        public bool AddRailToDictionary(Rail rail) {
            Point p0, p1;
            if (!rail.GetStartEnd(out p0, out p1)) return false;
            var st = new SymmetricSegment(p0, p1);
            if (!_railDictionary.ContainsKey(st)) {
                _railDictionary.Add(st, rail);
                return true;
            }
            return false;
        }

        public void AddRailToRtree(Rail rail) {
            Point p0, p1;
            if (!rail.GetStartEnd(out p0, out p1)) return;
            if (_railTree.Contains(new Rectangle(p0, p1), rail))
                return;
            _railTree.Add(new Rectangle(p0, p1), rail);
        }

        public void RemoveRailFromDictionary(Rail rail) {
            Point p0, p1;
            if (!rail.GetStartEnd(out p0, out p1)) return;
            _railDictionary.Remove(new SymmetricSegment(p0, p1));
        }

        #region Statistics

        class TileStatistic {
            public int vertices;
            public int rails;
        }

        #endregion


        internal void AddRail(Rail rail) {
            if (AddRailToDictionary(rail))
                AddRailToRtree(rail);
        }

        public void ClearRailTree() {
            _railTree.Clear();
        }

        public void CreateNodeTree(IEnumerable<LgNodeInfo> nodeInfos, double nodeDotWidth) {
            foreach (var n in nodeInfos)
                NodeInfoTree.Add(new RectangleNode<LgNodeInfo>(n,
                    new Rectangle(new Size(nodeDotWidth, nodeDotWidth),n.Center)));
        }

        public IEnumerable<Node> GetNodesIntersectingRect(Rectangle visibleRectangle) {
            return NodeInfoTree.GetAllIntersecting(visibleRectangle).Select(n => n.GeometryNode);
        }

        public bool RectIsEmptyOnLevel(Rectangle tileBox) {
            LgNodeInfo lg;
            return !NodeInfoTree.OneIntersecting(tileBox, out lg);
        }

        internal void RemoveFromRailEdges(List<SymmetricTuple<LgNodeInfo>> removeList) {
            foreach (var symmetricTuple in removeList)
                RemoveTupleFromRailEdges(symmetricTuple);
        }

        void RemoveTupleFromRailEdges(SymmetricTuple<LgNodeInfo> tuple) {
            var a = tuple.A.GeometryNode;
            var b = tuple.B.GeometryNode;
            foreach (var edge in EdgesBetween(a, b))
                _railsOfEdges.Remove(edge);
        }

        IEnumerable<Edge> EdgesBetween(Node a, Node b) {
            foreach (var e in a.InEdges.Where(e => e.Source == b))
                yield return e;
            foreach (var e in a.OutEdges.Where(e => e.Target == b))
                yield return e;
        }
    }
}