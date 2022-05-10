using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class DominosaScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public GameObject[] DominoImages;
    public Material[] DominoMats;
    public GameObject[] DominoGuides;

    public GameObject[] DominoObjs;
    public KMSelectable[] DominoSels;
    public KMSelectable ResetSel;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private List<DominoPair> _dominoPairs;

    private readonly int[] _gridValues = new int[56];
    private int? _prevDomino;
    private readonly List<int[]> _selectedDominoes = new List<int[]>();
    private readonly List<int[]> _selectedDominoValues = new List<int[]>();
    private List<Color>[] _dominoColors = new List<Color>[28];
    private Color[] _dominoGuideColors = new Color[28];
    private Coroutine[] _cycleDominoColors = new Coroutine[28];

    private const int _pips = 7; // numbers on dominoes go 0–(pips-1)
    private const int _w = _pips + 1;
    private const int _h = _pips;

    private sealed class DominoPair : IEquatable<DominoPair>
    {
        public int[] coords;
        public int[] numPair;

        public DominoPair(int[] coords, int[] numPair)
        {
            if (coords == null || coords.Length != 2)
                throw new ArgumentException("coords must be length 2.");
            if (numPair == null || numPair.Length != 2)
                throw new ArgumentException("numPair must be length 2.");
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

        ResetSel.OnInteract += ResetPress;
        for (int i = 0; i < DominoSels.Length; i++)
            DominoSels[i].OnInteract += DominoPress(i);
        for (int i = 0; i < 28; i++)
            _dominoColors[i] = new List<Color>();

        // GENERATE DOMINO ARRANGEMENT
        retry:
        var chosenPairs = Enumerable.Range(0, _pips).SelectMany(i => Enumerable.Range(i, _pips - i).Select(j => Rnd.Range(0, 2) != 0 ? new[] { i, j } : new[] { j, i })).ToArray().Shuffle();
        _dominoPairs = new List<DominoPair>();
        var taken = new bool[_w][] { new bool[_h], new bool[_h], new bool[_h], new bool[_h], new bool[_h], new bool[_h], new bool[_h], new bool[_h] };
        var px = 0;
        var py = 0;
        for (int i = 0; i < _w * _h / 2; i++)
        {
            while (taken[px][py])
            {
                px++;
                if (px == _w)
                {
                    py++;
                    px = 0;
                }
            }
            if (py == _h - 1 && (px == _w - 1 || taken[px + 1][py]))
                goto retry;
            var vert = px == _w - 1 || taken[px + 1][py] ? true : py == _h - 1 || taken[px][py + 1] ? false : Rnd.Range(0, 2) == 0;
            taken[px][py] = true;
            taken[vert ? px : px + 1][vert ? py + 1 : py] = true;
            var newDomino = new DominoPair(new[] { px + py * _w, vert ? px + py * _w + _w : px + py * _w + 1 }, chosenPairs[i]);
            _dominoPairs.Add(newDomino);
            _gridValues[newDomino.coords[0]] = newDomino.numPair[0];
            _gridValues[newDomino.coords[1]] = newDomino.numPair[1];
        }

        // CHECK FOR UNIQUE SOLUTION
        int[] c = FindSolutions(new bool[_w * _h], Enumerable.Range(0, _pips).SelectMany(i => Enumerable.Range(i, _pips - i).Select(j => new[] { i, j })).ToList(), 0).Take(2).ToArray();
        if (c.Length > 1 || c[0] > 3)
            goto retry;

        // SET TEXTURES
        for (int i = 0; i < _dominoPairs.Count; i++)
        {
            DominoImages[_dominoPairs[i].coords[0]].GetComponent<MeshRenderer>().sharedMaterial = DominoMats[_dominoPairs[i].numPair[0]];
            DominoImages[_dominoPairs[i].coords[1]].GetComponent<MeshRenderer>().sharedMaterial = DominoMats[_dominoPairs[i].numPair[1]];
        }
        var str = VisualizeGrid(_dominoPairs).Split('\n');
        Debug.LogFormat("[Dominosa #{0}] Solution:", _moduleId);
        for (int i = 0; i < str.Length; i++)
            Debug.LogFormat("[Dominosa #{0}] {1}", _moduleId, str[i]);
    }

    private string VisualizeGrid(IEnumerable<DominoPair> dominoes)
    {
        var ww = 4 * _w + 1;
        var hh = 2 * _h + 1;
        var lines = new int[ww * hh];
        foreach (var d in dominoes)
        {
            var tlx = Math.Min(d.coords[0] % _w, d.coords[1] % _w);
            var tly = Math.Min(d.coords[0] / _w, d.coords[1] / _w);
            if (d.coords[0] % _w == d.coords[1] % _w)
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
                var ix = x / 4 + _w * (y / 2);
                if (x % 4 == 2 && y % 2 == 1)
                    sb.Append(_gridValues[ix]);
                else
                    sb.Append(" ##└#│┌├#┘─┴┐┤┬┼"[lines[x + ww * y]]);
            }
            if (y != hh - 1)
                sb.Append(Environment.NewLine);
        }
        return sb.ToString();
    }

    private IEnumerable<int> FindSolutions(bool[] visited, List<int[]> availableDominoes, int hypotheses)
    {
        var givens = 0;
        keepChecking:
        if (availableDominoes.Count == 0)
        {
            yield return hypotheses;
            yield break;
        }

        // Check if any of the available dominoes has only one place to go
        for (var dIx = 0; dIx < availableDominoes.Count; dIx++)
        {
            var domino = availableDominoes[dIx];
            var c1 = -1;
            var c2 = -1;
            var count = 0;
            for (var ix = 0; ix < _w * _h; ix++)
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
                yield break;
            if (count == 1)
            {
                givens++;
                visited[c1] = true;
                visited[c2] = true;
                availableDominoes.RemoveAt(dIx);
                goto keepChecking;
            }
        }

        // Check if any position on the board can only accommodate one domino
        for (var ix = 0; ix < _w * _h; ix++)
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
                var dIx = availableDominoes.IndexOf(d => (d[0] == _gridValues[ix] && d[1] == _gridValues[adj]) || (d[1] == _gridValues[ix] && d[0] == _gridValues[adj]));
                if (dIx != -1)
                {
                    count++;
                    dominoIx = dIx;
                    adjIx = adj;
                }
            }
            if (count == 0)
                yield break;
            if (count == 1)
            {
                visited[ix] = true;
                visited[adjIx] = true;
                availableDominoes.RemoveAt(dominoIx);
                goto keepChecking;
            }
        }

        // Multiple possibilities exist ⇒ recurse
        // Start a hypothesis
        if (givens < 6)
            yield return 3;

        var firstUnvisited = Array.IndexOf(visited, false);

        foreach (var adj in GetAdjacents(firstUnvisited))
        {
            if (visited[adj])
                continue;
            var dIx = availableDominoes.IndexOf(d => (d[0] == _gridValues[firstUnvisited] && d[1] == _gridValues[adj]) || (d[1] == _gridValues[firstUnvisited] && d[0] == _gridValues[adj]));

            var availableDominosCopy = availableDominoes.ToList();
            availableDominosCopy.RemoveAt(dIx);

            var visitedCopy = visited.ToArray();
            visitedCopy[firstUnvisited] = true;
            visitedCopy[adj] = true;

            foreach (var solution in FindSolutions(visitedCopy, availableDominosCopy, hypotheses + 1))
                yield return solution;
        }
    }

    private KMSelectable.OnInteractHandler DominoPress(int i)
    {
        return delegate ()
        {
            if (_moduleSolved)
                return false;
            for (int j = 0; j < _selectedDominoes.Count; j++)
                if (_selectedDominoes[j] != null && _selectedDominoes[j].Contains(i))
                {
                    if (_prevDomino == null)
                    {
                        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                        var guideIx = (13 * _selectedDominoValues[j].Min() - _selectedDominoValues[j].Min() * _selectedDominoValues[j].Min()) / 2 + _selectedDominoValues[j].Max();
                        DominoObjs[_selectedDominoes[j][0]].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                        DominoObjs[_selectedDominoes[j][1]].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                        _selectedDominoes[j] = null;
                        _selectedDominoValues[j] = null;
                        _dominoColors[guideIx].RemoveAt(_dominoColors[guideIx].IndexOf(_dominoGuideColors[j]));
                        if (_cycleDominoColors[guideIx] != null)
                            StopCoroutine(_cycleDominoColors[guideIx]);
                        if (_dominoColors[guideIx].Count == 0)
                            DominoGuides[guideIx].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                        else if (_dominoColors[guideIx].Count > 1)
                            _cycleDominoColors[guideIx] = StartCoroutine(CycleDominoColors(guideIx));
                        else
                            DominoGuides[guideIx].GetComponent<MeshRenderer>().material.color = _dominoColors[guideIx][0];
                    }
                    else
                    {
                        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                        DominoObjs[_prevDomino.Value].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                        _prevDomino = null;
                    }
                    return false;
                }
            if (_prevDomino == null)
            {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                var ix = _selectedDominoes.IndexOf(null);
                var color = GetColor(ix == -1 ? _selectedDominoes.Count : ix);
                DominoObjs[i].GetComponent<MeshRenderer>().material.color = color;
                _prevDomino = i;
            }
            else
            {
                if (!GetAdjacents(i).Contains(_prevDomino.Value))
                {
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                    DominoObjs[_prevDomino.Value].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
                    _prevDomino = null;
                }
                else
                {
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
                    var ix = _selectedDominoes.IndexOf(null);
                    var color = GetColor(ix == -1 ? _selectedDominoes.Count : ix);
                    DominoObjs[i].GetComponent<MeshRenderer>().material.color = color;
                    if (ix == -1)
                    {
                        _selectedDominoes.Add(new[] { i, _prevDomino.Value });
                        _selectedDominoValues.Add(new[] { _gridValues[i], _gridValues[_prevDomino.Value] });
                        ix = _selectedDominoValues.Count - 1;
                    }
                    else
                    {
                        _selectedDominoes[ix] = (new[] { i, _prevDomino.Value });
                        _selectedDominoValues[ix] = (new[] { _gridValues[i], _gridValues[_prevDomino.Value] });
                    }
                    var guideIx = (13 * _selectedDominoValues[ix].Min() - _selectedDominoValues[ix].Min() * _selectedDominoValues[ix].Min()) / 2 + _selectedDominoValues[ix].Max();
                    _dominoGuideColors[ix] = color;
                    _dominoColors[guideIx].Add(color);
                    if (_cycleDominoColors[guideIx] != null)
                        StopCoroutine(_cycleDominoColors[guideIx]);
                    if (_dominoColors[guideIx].Count == 1)
                        DominoGuides[guideIx].GetComponent<MeshRenderer>().material.color = color;
                    else
                        _cycleDominoColors[guideIx] = StartCoroutine(CycleDominoColors(guideIx));
                    _prevDomino = null;
                    CheckForSolve();
                }
            }
            return false;
        };
    }

    private IEnumerator CycleDominoColors(int domino)
    {
        int ix = 0;
        while (true)
        {
            DominoGuides[domino].GetComponent<MeshRenderer>().material.color = _dominoColors[domino][ix];
            ix = (ix + 1) % _dominoColors[domino].Count;
            yield return new WaitForSeconds(0.4f);
        }
    }

    private Color GetColor(int ix)
    {
        return Color.HSVToRGB((ix * 0.175f) % 1f, 0.8f, 0.8f);
    }

    private IEnumerable<int> GetAdjacents(int ix)
    {
        if (ix % _w != 0)
            yield return ix - 1;
        if (ix % _w != 7)
            yield return ix + 1;
        if (ix / _w != 0)
            yield return ix - 8;
        if (ix / _w != 6)
            yield return ix + 8;
    }

    private void CheckForSolve()
    {
        if (_selectedDominoValues.Count != 28)
            return;
        var list = new List<string>();
        for (int i = 0; i < _selectedDominoValues.Count; i++)
        {
            var arr = (_selectedDominoValues[i][0] > _selectedDominoValues[i][1] ? new[] { _selectedDominoValues[i][1], _selectedDominoValues[i][0] } : new[] { _selectedDominoValues[i][0], _selectedDominoValues[i][1] }).Join("");
            if (!list.Contains(arr))
                list.Add(arr);
        }
        if (list.Count == 28)
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
            _moduleSolved = true;
            Module.HandlePass();
        }
    }

    private bool ResetPress()
    {
        if (_moduleSolved)
            return false;
        _prevDomino = null;
        for (int j = 0; j < _selectedDominoes.Count; j++)
        {
            if (_selectedDominoes[j] == null)
                continue;
            var guideIx = (13 * _selectedDominoValues[j].Min() - _selectedDominoValues[j].Min() * _selectedDominoValues[j].Min()) / 2 + _selectedDominoValues[j].Max();
            DominoObjs[_selectedDominoes[j][0]].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
            DominoObjs[_selectedDominoes[j][1]].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
            _selectedDominoes[j] = null;
            _selectedDominoValues[j] = null;
            _dominoColors[guideIx].RemoveAt(_dominoColors[guideIx].IndexOf(_dominoGuideColors[j]));
            if (_cycleDominoColors[guideIx] != null)
                StopCoroutine(_cycleDominoColors[guideIx]);
            DominoGuides[guideIx].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
        }
        for (int i = 0; i < 56; i++)
            DominoObjs[i].GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 255);
        return false;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = @"!{0} a1 a2 b3 b4 [Press dominoes a1, a2, b3, b4.] | !{0} reset [Presses reset button.]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*reset\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            ResetSel.OnInteract();
            yield break;
        }
        var parameters = command.ToUpperInvariant().Split(' ');
        var list = new List<int>();
        for (int i = parameters[0] == "PRESS" ? 1 : 0; i < parameters.Length; i++)
        {
            int val;
            if (parameters[i].Length != 2 || !((parameters[i][0] >= 'A') && (parameters[i][0] <= 'H')) || !int.TryParse(parameters[i].Substring(1), out val) || val < 1 || val > 7)
            {
                yield return "sendtochaterror " + parameters[i] + " is not a valid command! Press a domino cell with a letter-number coordinate.";
                yield break;
            }
            list.Add(parameters[i][0] - 'A' + (val - 1) * 8);
        }
        yield return null;
        for (int i = 0; i < list.Count; i++)
        {
            DominoSels[list[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        ResetSel.OnInteract();
        yield return new WaitForSeconds(0.1f);
        for (int i = 0; i < _dominoPairs.Count; i++)
            for (int j = 0; j < 2; j++)
            {
                DominoSels[_dominoPairs[i].coords[j]].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
    }
}
