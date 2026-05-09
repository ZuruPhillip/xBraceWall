namespace CncWallStation.Plcs
{
    /// <summary>
    /// 共线等间距合并器（通用）
    ///
    /// 职责：
    ///   将一组带 (x, y) 坐标的元素，按"等间距共线"原则贪心合并为若干链。
    ///   每条链可作为一条带 D 复制次数的 PLC 指令发射。
    ///
    /// 算法：
    ///   1. 标记数组 used[]
    ///   2. 对每个未使用元素 i：
    ///        遍历其它候选 j 计算方向 (dx, dy)
    ///        沿该方向连续查找等间距点，构造最长链
    ///        选取最长链作为 i 的归属链
    ///   3. 重复直到所有元素被覆盖
    ///
    /// 时间复杂度：O(N²)（N 为单组元素数，工程场景 N 通常 < 50）
    /// </summary>
    public static class CollinearMerger
    {
        /// <summary>
        /// 单条合并结果
        /// </summary>
        public readonly record struct Chain<T>(
            T First,           // 链首元素（PLC 指令的 X0/Y0/Z0 来源）
            int CopyCount,     // 复制次数 = 链长 - 1（即 PLC 的 D 值）
            float Dx,          // X 方向间距（PLC 的 X1）
            float Dy);         // Y 方向间距（PLC 的 Y1）

        /// <summary>
        /// 合并入口
        /// </summary>
        /// <param name="items">同一等价组内的元素（已确保 T/F/规格相同）</param>
        /// <param name="posOf">坐标提取器：item → (x, y)</param>
        /// <param name="tol">位置容差（mm）</param>
        public static IEnumerable<Chain<T>> Merge<T>(
            IList<T> items,
            Func<T, (float x, float y)> posOf,
            float tol = 1f)
        {
            if (items == null || items.Count == 0) yield break;

            var used = new bool[items.Count];
            var pts = new (float x, float y)[items.Count];
            for (int i = 0; i < items.Count; i++) pts[i] = posOf(items[i]);

            for (int i = 0; i < items.Count; i++)
            {
                if (used[i]) continue;

                var (chain, dx, dy) = FindLongestChain(pts, used, i, tol);
                foreach (int idx in chain) used[idx] = true;

                yield return new Chain<T>(
                    First: items[chain[0]],
                    CopyCount: chain.Count - 1,
                    Dx: dx,
                    Dy: dy);
            }
        }

        // ──────────────────────────────────────────────────
        // 内部：贪心查找最长共线等间距序列
        // ──────────────────────────────────────────────────

        private static (List<int> chain, float dx, float dy) FindLongestChain(
            (float x, float y)[] pts, bool[] used, int startIdx, float tol)
        {
            List<int> best = new() { startIdx };
            float bestDx = 0, bestDy = 0;

            for (int j = 0; j < pts.Length; j++)
            {
                if (j == startIdx || used[j]) continue;

                float dx = pts[j].x - pts[startIdx].x;
                float dy = pts[j].y - pts[startIdx].y;

                var chain = new List<int> { startIdx, j };
                float curX = pts[j].x;
                float curY = pts[j].y;

                while (true)
                {
                    float nextX = curX + dx;
                    float nextY = curY + dy;

                    int nextIdx = FindAt(pts, used, chain, nextX, nextY, tol);
                    if (nextIdx < 0) break;

                    chain.Add(nextIdx);
                    curX = nextX;
                    curY = nextY;
                }

                if (chain.Count > best.Count)
                {
                    best = chain;
                    bestDx = dx;
                    bestDy = dy;
                }
            }

            return (best, bestDx, bestDy);
        }

        private static int FindAt(
            (float x, float y)[] pts, bool[] used, List<int> chain,
            float x, float y, float tol)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                if (used[i] || chain.Contains(i)) continue;
                if (MathF.Abs(pts[i].x - x) <= tol &&
                    MathF.Abs(pts[i].y - y) <= tol)
                    return i;
            }
            return -1;
        }
    }
}