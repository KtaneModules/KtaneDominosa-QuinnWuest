using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private int[][] _possibleCombos;
    private List<DominoPair> _dominoPairs;

    private int[] _gridValues = new int[56];
    private int? _prevDomino;
    private List<int[]> _selectedDominos = new List<int[]>();
    private List<int[]> _selectedDominoValues = new List<int[]>();


    private sealed class DominoPair : IEquatable<DominoPair>
    {
        public int index;
        public int[] coords;
        public int[] numPair;

        public DominoPair(int index, int[] coords, int[] numPair)
        {
            this.index = index;
            this.coords = coords;
            this.numPair = numPair;
        }

        public bool Equals(DominoPair other)
        {
            return other != null & other.index == index && other.coords.SequenceEqual(coords) && other.numPair.SequenceEqual(numPair);
        }
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        _possibleCombos = SetupPairs();

        for (int i = 0; i < DominoSels.Length; i++)
            DominoSels[i].OnInteract += DominoPress(i);

        // GENERATION (with references from Wire Placement)
        // Currently does not check for unique solutions.
        var chosenPairs = _possibleCombos.Select(i => i).ToArray().Shuffle();
        retry:
        _dominoPairs = new List<DominoPair>();
        var taken = new bool[8][] { new bool[7], new bool[7], new bool[7], new bool[7], new bool[7], new bool[7], new bool[7], new bool[7] };
        var px = 0;
        var py = 0;
        for (int i = 0; i < 28; i++)
        {
            while (taken[px][py])
            {
                px++;
                if (px == 8)
                {
                    py++;
                    px = 0;
                }
            }
            if (py == 6 && (px == 7 || taken[px + 1][py]))
                goto retry;
            var vert = px == 7 || taken[px + 1][py] ? true : py == 6 || taken[px][py + 1] ? false : Rnd.Range(0, 2) == 0;
            taken[px][py] = true;
            taken[vert ? px : px + 1][vert ? py + 1 : py] = true;
            _dominoPairs.Add(new DominoPair( i, new[] { px + py * 8, vert ? px + py * 8 + 8 : px + py * 8 + 1 }, chosenPairs[i]));
        }
        // END GENERATION

        // SET NUMBERS
        for (int i = 0; i < _dominoPairs.Count; i++)
        {
            DominoImages[_dominoPairs[i].coords[0]].GetComponent<MeshRenderer>().material.mainTexture = DominoTextures[_dominoPairs[i].numPair[0]];
            DominoImages[_dominoPairs[i].coords[1]].GetComponent<MeshRenderer>().material.mainTexture = DominoTextures[_dominoPairs[i].numPair[1]];
            _gridValues[_dominoPairs[i].coords[0]] = _dominoPairs[i].numPair[0];
            _gridValues[_dominoPairs[i].coords[1]] = _dominoPairs[i].numPair[1];
        }
        // END SET NUMBERS
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

    private int[][] SetupPairs()
    {
        var list = new List<int[]>();
        for (int i = 0; i < 7; i++)
            for (int j = i; j < 7; j++)
                list.Add(new[] { i, j });
        return list.ToArray();
    }

    private List<int> GetAdjacents(int num)
    {
        var list = new List<int>();
        if (num % 8 != 0)
            list.Add(num - 1);
        if (num % 8 != 7)
            list.Add(num + 1);
        if (num / 8 != 0)
            list.Add(num - 8);
        if (num / 8 != 6)
            list.Add(num + 8);
        return list;
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
