using System;
using System.Collections.Generic;
using System.Linq;

namespace DopravniSit
{
    // Generické typy:
    // K: Klíč uzlu (např. string "z") [cite: 63]
    // V: Data uzlu (např. souřadnice Point) [cite: 63]
    // E: Data hrany (např. název silnice)
    // W: Váha hrany (např. double čas) [cite: 64]
    public abstract class AbstrGraph<K, V, E, W> where K : IComparable<K> where W : IComparable<W>
    {
        // Oprava: V AbstrGraph změňte typ uzlů v BST na Node<K, V, E, W>
        protected BinarySearchTree<K, Node<K, V, E, W>> NodesBST = new BinarySearchTree<K, Node<K, V, E, W>>();

        // Abstraktní metody pro práci s vahami (sčítání času, porovnání)
        protected abstract W Zero { get; }
        protected abstract W MaxValue { get; }
        protected abstract W AddWeights(W a, W b);

        // Oprava: V metodě AddNode upravte konstrukci uzlu na generickou verzi
        public void AddNode(K key, V data)
        {
            NodesBST.Insert(key, new Node<K, V, E, W>(key, data));
        }

        // Neorientovaný graf: hrana A->B implikuje B->A [cite: 8, 62]
        public void AddEdge(K sourceKey, K targetKey, E edgeData, W weight)
        {
            var sourceNode = NodesBST.Find(sourceKey);
            var targetNode = NodesBST.Find(targetKey);

            if (sourceNode != null && targetNode != null)
            {
                // Přidání obousměrně

                sourceNode.Edges.Add(new Edge<K, E, W>(targetKey, edgeData, weight));
                targetNode.Edges.Add(new Edge<K, E, W>(sourceKey, edgeData, weight));
            }
        }

        // Oprava: V metodě GetNode upravte návratový typ na Node<K, V, E, W>
        public Node<K, V, E, W> GetNode(K key) => NodesBST.Find(key);

        // Oprava: V metodě GetAllNodes upravte návratový typ na List<Node<K, V, E, W>>
        public List<Node<K, V, E, W>> GetAllNodes() => NodesBST.InOrderTraversal();

        // Dijkstrův algoritmus [cite: 5]
        // Vrací mapu předchůdců (Vektor následníků/předchůdců) [cite: 9, 61]
        public Dictionary<K, K> Dijkstra(K startKey, K endKey, HashSet<(K, K)> blockedEdges, out Dictionary<K, W> distances)
        {
            distances = new Dictionary<K, W>();
            var previous = new Dictionary<K, K>();
            var nodes = GetAllNodes();

            // Inicializace
            foreach (var node in nodes)
            {
                distances[node.Key] = MaxValue;
            }
            distances[startKey] = Zero;

            // Prioritní fronta (.NET 6+)
            var pq = new PriorityQueue<K, W>();
            pq.Enqueue(startKey, Zero);

            while (pq.Count > 0)
            {
                K currentKey = pq.Dequeue();

                if (currentKey.CompareTo(endKey) == 0) break; // Cíl nalezen

                var currentNode = NodesBST.Find(currentKey);
                if (currentNode == null) continue;

                foreach (var edge in currentNode.Edges)
                {
                    // Kontrola "problematických" (blokovaných) hran [cite: 7, 72]
                    if (blockedEdges.Contains((currentKey, edge.TargetKey)) ||
                        blockedEdges.Contains((edge.TargetKey, currentKey)))
                        continue;

                    W newDist = AddWeights(distances[currentKey], edge.Weight);

                    if (newDist.CompareTo(distances[edge.TargetKey]) < 0)
                    {
                        distances[edge.TargetKey] = newDist;
                        previous[edge.TargetKey] = currentKey;
                        pq.Enqueue(edge.TargetKey, newDist);
                    }
                }
            }
            return previous;
        }
    }

    // Oprava: Node<K, V, E, W> musí být generická třída, aby bylo možné použít typy E a W
    public class Node<K, V, E, W>
    {
        public K Key { get; set; }
        public V Data { get; set; }
        public List<Edge<K, E, W>> Edges { get; set; } = new();

        public Node(K key, V data) { Key = key; Data = data; }
    }

    public class Edge<K, E, W>
    {
        public K TargetKey { get; set; }
        public E Data { get; set; }
        public W Weight { get; set; } // Čas průjezdu [cite: 64]
        public Edge(K target, E data, W weight) { TargetKey = target; Data = data; Weight = weight; }
    }

    // Implementace Binárního Vyhledávacího Stromu (BST)
    public class BinarySearchTree<K, T> where K : IComparable<K>
    {
        private class BSTNode
        {
            public K Key;
            public T Value;
            public BSTNode Left, Right;
            public BSTNode(K key, T value) { Key = key; Value = value; }
        }

        private BSTNode root;

        public void Insert(K key, T value)
        {
            root = InsertRec(root, key, value);
        }

        private BSTNode InsertRec(BSTNode root, K key, T value)
        {
            if (root == null) return new BSTNode(key, value);
            if (key.CompareTo(root.Key) < 0) root.Left = InsertRec(root.Left, key, value);
            else if (key.CompareTo(root.Key) > 0) root.Right = InsertRec(root.Right, key, value);
            return root;
        }

        public T Find(K key)
        {
            return FindRec(root, key);
        }

        private T FindRec(BSTNode root, K key)
        {
            if (root == null) return default(T);
            if (key.CompareTo(root.Key) == 0) return root.Value;
            if (key.CompareTo(root.Key) < 0) return FindRec(root.Left, key);
            return FindRec(root.Right, key);
        }

        public List<T> InOrderTraversal()
        {
            var list = new List<T>();
            InOrderRec(root, list);
            return list;
        }

        private void InOrderRec(BSTNode root, List<T> list)
        {
            if (root != null)
            {
                InOrderRec(root.Left, list);
                list.Add(root.Value);
                InOrderRec(root.Right, list);
            }
        }
    }
}