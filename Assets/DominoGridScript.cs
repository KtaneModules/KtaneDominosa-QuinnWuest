using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

using Rnd = UnityEngine.Random;

public class DominoGridScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public GameObject[] DominoObjs;
    public GameObject[] DominoImages;
    public Texture[] DominoTextures;
    public KMSelectable[] DominoSels;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private List<DominoPair> _dominoPairs;

    private readonly int[] _gridValues = new int[56];
    private int? _prevDomino;
    private readonly List<int[]> _selectedDominos = new List<int[]>();
    private readonly List<int[]> _selectedDominoValues = new List<int[]>();

    private const int pips = 7; // numbers on dominos go 0–(pips-1)
    private const int w = pips + 1;
    private const int h = pips;

    private sealed class DominoPair : IEquatable<DominoPair>
    {
        public int[] coords;
        public int[] numPair;

        public DominoPair(int[] coords, int[] numPair)
        {
            if (coords == null || coords.Length != 2)
                throw new ArgumentException("‘coords’ must be length 2.");
            if (numPair == null || numPair.Length != 2)
                throw new ArgumentException("‘numPair’ must be length 2.");

            Array.Sort(numPair, coords);
            this.coords = coords;
            this.numPair = numPair;
        }

        public bool Equals(DominoPair other)
        {
            return other != null && other.coords.SequenceEqual(coords) && other.numPair.SequenceEqual(numPair);
        }

        public override int GetHashCode()
        {
            return coords[0] + 37 * coords[1] + 47 * coords[2] + 101 * coords[3];
        }
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < DominoSels.Length; i++)
            DominoSels[i].OnInteract += DominoPress(i);

        // GENERATE DOMINO ARRANGEMENT
        retry:
        var chosenPairs = Enumerable.Range(0, pips).SelectMany(i => Enumerable.Range(i, pips - i).Select(j => Rnd.Range(0, 2) != 0 ? new[] { i, j } : new[] { j, i })).ToArray().Shuffle();
        _dominoPairs = new List<DominoPair>();
        var taken = new bool[w][] { new bool[h], new bool[h], new bool[h], new bool[h], new bool[h], new bool[h], new bool[h], new bool[h] };
        var px = 0;
        var py = 0;
        for (int i = 0; i < w * h / 2; i++)
        {
            while (taken[px][py])
            {
                px++;
                if (px == w)
                {
                    py++;
                    px = 0;
                }
            }
            if (py == h - 1 && (px == w - 1 || taken[px + 1][py]))
                goto retry;
            var vert = px == w - 1 || taken[px + 1][py] ? true : py == h - 1 || taken[px][py + 1] ? false : Rnd.Range(0, 2) == 0;
            taken[px][py] = true;
            taken[vert ? px : px + 1][vert ? py + 1 : py] = true;
            var newDomino = new DominoPair(new[] { px + py * w, vert ? px + py * w + w : px + py * w + 1 }, chosenPairs[i]);
            _dominoPairs.Add(newDomino);
            _gridValues[newDomino.coords[0]] = newDomino.numPair[0];
            _gridValues[newDomino.coords[1]] = newDomino.numPair[1];
        }

        // DEBUG LOG
        File.WriteAllText(@"D:\temp\temp.txt", VisualizeGrid(new bool[w * h], _dominoPairs));

        // CHECK FOR UNIQUE SOLUTION
        var c = FindSolutions(new bool[w * h], Enumerable.Range(0, pips).SelectMany(i => Enumerable.Range(i, pips - i).Select(j => new[] { i, j })).ToList()).Take(2).Count();
        if (c == 0)
            throw new InvalidOperationException("Bah");
        if (c > 1)
            goto retry;

        // SET TEXTURES
        for (int i = 0; i < _dominoPairs.Count; i++)
        {
            DominoImages[_dominoPairs[i].coords[0]].GetComponent<MeshRenderer>().material.mainTexture = DominoTextures[_dominoPairs[i].numPair[0]];
            DominoImages[_dominoPairs[i].coords[1]].GetComponent<MeshRenderer>().material.mainTexture = DominoTextures[_dominoPairs[i].numPair[1]];
        }
    }

    private string VisualizeGrid(bool[] visited, IEnumerable<DominoPair> dominos)
    {
        var ww = 4 * w + 1;
        var hh = 2 * h + 1;
        var lines = new int[ww * hh];
        foreach (var d in dominos)
        {
            var tlx = Math.Min(d.coords[0] % w, d.coords[1] % w);
            var tly = Math.Min(d.coords[0] / w, d.coords[1] / w);
            if (d.coords[0] % w == d.coords[1] % w)
            {
                for (var x = 0; x < 4; x++)
                {
                    lines[4 * tlx + x + ww * (2 * tly)] |= 2;
                    lines[4 * tlx + x + 1 + ww * (2 * tly)] |= 8;
                    lines[4 * tlx + x + ww * (2 * tly + 4)] |= 2;
                    lines[4 * tlx + x + 1 + ww * (2 * tly + 4)] |= 8;
                }
                for (var y = 0; y < 4; y++)
                {
                    lines[(4 * tlx) + ww * (2 * tly + y)] |= 4;
                    lines[(4 * tlx) + ww * (2 * tly + y + 1)] |= 1;
                    lines[(4 * tlx + 4) + ww * (2 * tly + y)] |= 4;
                    lines[(4 * tlx + 4) + ww * (2 * tly + y + 1)] |= 1;
                }
            }
            else
            {
                for (var x = 0; x < 8; x++)
                {
                    lines[4 * tlx + x + ww * (2 * tly)] |= 2;
                    lines[4 * tlx + x + 1 + ww * (2 * tly)] |= 8;
                    lines[4 * tlx + x + ww * (2 * tly + 2)] |= 2;
                    lines[4 * tlx + x + 1 + ww * (2 * tly + 2)] |= 8;
                }
                for (var y = 0; y < 2; y++)
                {
                    lines[(4 * tlx) + ww * (2 * tly + y)] |= 4;
                    lines[(4 * tlx) + ww * (2 * tly + y + 1)] |= 1;
                    lines[(4 * tlx + 8) + ww * (2 * tly + y)] |= 4;
                    lines[(4 * tlx + 8) + ww * (2 * tly + y + 1)] |= 1;
                }
            }
        }
        var sb = new StringBuilder();
        for (var y = 0; y < hh; y++)
        {
            for (var x = 0; x < ww; x++)
            {
                var ix = x / 4 + w * (y / 2);
                if (x % 4 == 1 && y % 2 == 1 && visited[ix])
                    sb.Append("(");
                else if (x % 4 == 2 && y % 2 == 1)
                    sb.Append(_gridValues[ix]);
                else if (x % 4 == 3 && y % 2 == 1 && visited[ix])
                    sb.Append(")");
                else
                    sb.Append(" ##└#│┌├#┘─┴┐┤┬┼"[lines[x + ww * y]]);
            }
            sb.Append(Environment.NewLine);
        }
        return sb.ToString();
    }

    private IEnumerable<object> FindSolutions(bool[] visited, List<int[]> availableDominos)
    {
        keepChecking:
        if (availableDominos.Count == 0)
        {
            File.AppendAllText(@"D:\temp\temp.txt", "Solution found!\n");
            yield return null;
            yield break;
        }

        // Check if any of the available dominos has only one place to go
        for (var dIx = 0; dIx < availableDominos.Count; dIx++)
        {
            var domino = availableDominos[dIx];
            var c1 = -1;
            var c2 = -1;
            var count = 0;
            for (var ix = 0; ix < w * h; ix++)
            {
                if (visited[ix] || _gridValues[ix] != domino[0])
                    continue;
                foreach (var adj in GetAdjacents(ix))
                {
                    if (adj < ix && _gridValues[adj] == _gridValues[ix])
                        continue;
                    if (!visited[adj] && _gridValues[adj] == domino[1])
                    {
                        count++;
                        c1 = ix;
                        c2 = adj;
                    }
                }
            }
            if (count == 0)
            {
                File.AppendAllText(@"D:\temp\temp.txt", string.Format("The {0}{1} domino cannot go anywhere anymore. Backtracking.{2}",
                    availableDominos[dIx][0], availableDominos[dIx][1], Environment.NewLine));
                yield break;
            }
            if (count == 1)
            {
                File.AppendAllText(@"D:\temp\temp.txt", string.Format("The {0}{1} domino can only go here.{2}{3}Remaining dominos: {4}{2}{2}",
                    availableDominos[dIx][0], availableDominos[dIx][1], Environment.NewLine, VisualizeGrid(visited, new[] { new DominoPair(new[] { c1, c2 }, availableDominos[dIx]) }),
                    Enumerable.Range(0, availableDominos.Count).Where(i => i != dIx).Select(i => availableDominos[i].Join("")).Join(", ")));
                visited[c1] = true;
                visited[c2] = true;
                availableDominos.RemoveAt(dIx);
                goto keepChecking;
            }
        }

        // Check if any position on the board can only accommodate one domino
        for (var ix = 0; ix < w * h; ix++)
        {
            if (visited[ix])
                continue;
            var dominoIx = -1;
            var adjIx = -1;
            var count = 0;
            foreach (var adj in GetAdjacents(ix))
            {
                if (visited[adj])
                    continue;
                var dIx = availableDominos.IndexOf(d => (d[0] == _gridValues[ix] && d[1] == _gridValues[adj]) || (d[1] == _gridValues[ix] && d[0] == _gridValues[adj]));
                if (dIx != -1)
                {
                    count++;
                    dominoIx = dIx;
                    adjIx = adj;
                }
            }
            if (count == 0)
            {
                File.AppendAllText(@"D:\temp\temp.txt", string.Format("Position {0}{1} is unsatisfiable. Backtracking.{2}", (char) ('A' + ix % w), ix / w + 1, Environment.NewLine));
                yield break;
            }
            if (count == 1)
            {
                File.AppendAllText(@"D:\temp\temp.txt", string.Format("Position {0}{1} can only take the {3} domino.{2}{4}Remaining dominos: {5}{2}{2}",
                    (char) ('A' + ix % w), ix / w + 1, Environment.NewLine, availableDominos[dominoIx].Join(""), VisualizeGrid(visited, new[] { new DominoPair(new[] { ix, adjIx }, availableDominos[dominoIx]) }),
                    Enumerable.Range(0, availableDominos.Count).Where(i => i != dominoIx).Select(i => availableDominos[i].Join("")).Join(", ")));
                visited[ix] = true;
                visited[adjIx] = true;
                availableDominos.RemoveAt(dominoIx);
                goto keepChecking;
            }
        }

        // Multiple possibilities exist ⇒ recurse
        var firstUnvisited = Array.IndexOf(visited, false);
        if (firstUnvisited == -1)
            throw new InvalidOperationException("There’s a bug in my algo, dear Timwi, dear Timwi! There’s a bug in my algo, dear Timwi, a bug!");

        foreach (var adj in GetAdjacents(firstUnvisited))
        {
            if (visited[adj])
                continue;
            var dIx = availableDominos.IndexOf(d => (d[0] == _gridValues[firstUnvisited] && d[1] == _gridValues[adj]) || (d[1] == _gridValues[firstUnvisited] && d[0] == _gridValues[adj]));
            if (dIx == -1)
                throw new InvalidOperationException("Well then fix it, dear Timwi, dear Timwi, dear Timwi! Well then fix it, dear Timwi, dear Timwi, fix it!");

            var availableDominosCopy = availableDominos.ToList();
            availableDominosCopy.RemoveAt(dIx);

            File.AppendAllText(@"D:\temp\temp.txt", string.Format("Hypothesis:{2}{0}Remaining dominos: {1}{2}{2}",
                VisualizeGrid(visited, new[] { new DominoPair(new[] { firstUnvisited, adj }, availableDominos[dIx]) }),
                availableDominosCopy.Select(d => d.Join("")).Join(", "),
                Environment.NewLine));

            var visitedCopy = visited.ToArray();
            visitedCopy[firstUnvisited] = true;
            visitedCopy[adj] = true;

            foreach (var solution in FindSolutions(visitedCopy, availableDominosCopy))
                yield return solution;
        }
    }

    private KMSelectable.OnInteractHandler DominoPress(int i)
    {
        return delegate ()
        {
            if (_moduleSolved)
                return false;
            for (int j = 0; j < _selectedDominos.Count; j++)
            {
                var arr = new[] { _selectedDominos[j][0], _selectedDominos[j][1] };
                if (arr.Contains(i))
                {
                    if (_prevDomino == null)
                    {
                        DominoObjs[_selectedDominos[j][0]].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                        DominoObjs[_selectedDominos[j][1]].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                        _selectedDominos.RemoveAt(j);
                        _selectedDominoValues.RemoveAt(j);
                    }
                    else
                    {
                        DominoObjs[_prevDomino.Value].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                        _prevDomino = null;
                    }
                    return false;
                }
            }
            if (_prevDomino == null)
            {
                var colors = GetColors();
                DominoObjs[i].GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(colors[0], colors[1], colors[2]);
                _prevDomino = i;
            }
            else
            {
                if (!GetAdjacents(i).Contains(_prevDomino.Value))
                {
                    DominoObjs[_prevDomino.Value].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                    _prevDomino = null;
                }
                else
                {
                    var colors = GetColors();
                    DominoObjs[i].GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(colors[0], colors[1], colors[2]);
                    _selectedDominos.Add(new[] { i, _prevDomino.Value });
                    _selectedDominoValues.Add(new[] { _gridValues[i], _gridValues[_prevDomino.Value] });
                    CheckForSolve();
                    _prevDomino = null;
                }
            }
            return false;
        };
    }

    private float[] GetColors()
    {
        return new float[] { _selectedDominos.Count * 0.035f, 0.8f, 0.8f };
    }

    private IEnumerable<int> GetAdjacents(int ix)
    {
        if (ix % w != 0)
            yield return ix - 1;
        if (ix % w != 7)
            yield return ix + 1;
        if (ix / w != 0)
            yield return ix - 8;
        if (ix / w != 6)
            yield return ix + 8;
    }

    private void CheckForSolve()
    {
        if (_selectedDominoValues.Count != 28)
            return;
        var list = new List<string>();
        for (int i = 0; i < _selectedDominoValues.Count; i++)
        {
            var arr = SortArr(new[] { _selectedDominoValues[i][0], _selectedDominoValues[i][1] }).Join("");
            if (!list.Contains(arr))
                list.Add(arr);
        }
        if (list.Count == 28)
        {
            _moduleSolved = true;
            Module.HandlePass();
        }
    }

    private int[] SortArr(int[] arr)
    {
        if (arr[0] > arr[1])
            return new[] { arr[1], arr[0] };
        return arr;
    }
}
