using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Set;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of S.E.T.
/// Created by Zawu and Timwi
/// </summary>
public class SetModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public Texture[] Symbols;
    public Texture[] Dots;
    public Texture[] SymbolsSelected;
    public Texture[] DotsSelected;
    public KMSelectable[] Cards;
    public KMSelectable MainSelectable;

    public MeshRenderer[] CardImages;
    public MeshRenderer[] CardDots;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private readonly List<int> _selected = new List<int>();
    private int[] _symbols;
    private SetSet _solution;
    private SetCard[] _displayedCards;
    private bool _isSolved;

    struct SetCard : IEquatable<SetCard>
    {
        public int Index { get; private set; }
        public int X => Index % 3;
        public int Y => (Index / 3) % 3;
        public int NumDots => (Index / 9) % 3;
        public int Filling => Index / 27;

        public SetCard(int x, int y, int numDots, int filling) { Index = x + 3 * (y + 3 * (numDots + 3 * filling)); }
        public SetCard(int index) { Index = index; }

        private static int get3rdValue(int one, int two) { return (6 - one - two) % 3; }
        public SetCard Get3rdInSet(SetCard other) { return new SetCard(get3rdValue(X, other.X), get3rdValue(Y, other.Y), get3rdValue(NumDots, other.NumDots), get3rdValue(Filling, other.Filling)); }
        public static SetCard GetRandom() { return new SetCard(Rnd.Range(0, 81)); }
        public bool Equals(SetCard other) { return other.Index == Index; }
        public override int GetHashCode() { return Index; }
        public override bool Equals(object obj) { return obj is SetCard && Equals((SetCard) obj); }
        public override string ToString() => $"X={X}, Y={Y}, NumDots={NumDots}, Filling={Filling}";
    }

    sealed class SetSet : IEquatable<SetSet>
    {
        public SetCard One { get; private set; }
        public SetCard Two { get; private set; }
        public SetCard Three { get; private set; }
        public IEnumerable<SetCard> Cards { get { yield return One; yield return Two; yield return Three; } }
        public SetSet(SetCard one, SetCard two, SetCard three)
        {
            One = one;
            Two = two;
            Three = three;
        }
        public override int GetHashCode()
        {
            // The hash function needs to be invariant under reordering of the three cards
            return (One.GetHashCode() + Two.GetHashCode() + Three.GetHashCode()) ^ (One.GetHashCode() * Two.GetHashCode() * Three.GetHashCode());
        }
        public bool Equals(SetSet other) { return other != null && other.Cards.Contains(One) && other.Cards.Contains(Two) && other.Cards.Contains(Three); }
        public override bool Equals(object obj) { return obj is SetSet && Equals((SetSet) obj); }

        public static SetSet GetRandom(bool avoidSameSymbol = false)
        {
            var one = SetCard.GetRandom();
            tryAgain:
            var two = SetCard.GetRandom();
            if (two.Equals(one) || (avoidSameSymbol && one.X == two.X && one.Y == two.Y))
                goto tryAgain;
            return new SetSet(one, two, one.Get3rdInSet(two));
        }
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < Cards.Length; i++)
            Cards[i].OnInteract = getClickHandler(i);

        var rnd = RuleSeedable.GetRNG();
        _symbols = rnd.Seed == 1
            ? Enumerable.Range(0, 9).ToArray()
            : rnd.ShuffleFisherYates(Enumerable.Range(0, Symbols.Length / 3).ToArray()).Take(9).ToArray();

        Debug.LogFormat("[S.E.T. #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);

        tryAgain:
        _solution = SetSet.GetRandom(avoidSameSymbol: true);
        var cards = new HashSet<SetCard> { _solution.One, _solution.Two, _solution.Three };
        var iter = 0;
        while (cards.Count < 9)
        {
            var newCard = SetCard.GetRandom();
            if (cards.Contains(newCard) || cards.Any(c => cards.Contains(newCard.Get3rdInSet(c))))
            {
                iter++;
                if (iter > 1000)
                    goto tryAgain;
                continue;
            }
            cards.Add(newCard);
        }

        _displayedCards = cards.ToList().Shuffle().ToArray();
        Debug.LogFormat("[S.E.T. #{0}] Icons in manual: {1}", _moduleId, _symbols.JoinString(", "));
        Debug.LogFormat("[S.E.T. #{0}] Icons on module: {1}", _moduleId, _displayedCards.Select(card => $"{card.X + 3 * card.Y}/{card.Filling}/{card.NumDots}").JoinString(", "));
        for (int i = 0; i < _displayedCards.Length; i++)
            SetSymbol(i, false);
        Debug.LogFormat("[S.E.T. #{0}] Solution: {1}, {2}, {3}.",
            _moduleId, coords(Array.IndexOf(_displayedCards, _solution.One)), coords(Array.IndexOf(_displayedCards, _solution.Two)), coords(Array.IndexOf(_displayedCards, _solution.Three)));
    }

    private void SetSymbol(int btn, bool selected)
    {
        var setCard = _displayedCards[btn];
        var sym = selected ? SymbolsSelected : Symbols;
        var dots = selected ? DotsSelected : Dots;

        CardDots[btn].gameObject.SetActive(setCard.NumDots > 0);
        CardDots[btn].material.mainTexture = setCard.NumDots > 0 ? dots[setCard.NumDots - 1] : null;
        CardImages[btn].material.mainTexture = sym[setCard.Filling + 3 * _symbols[setCard.X + 3 * setCard.Y]];
    }

    private string coords(int i) => $"{(char) ('A' + i % 3)}{(char) ('1' + i / 3)}";

    private KMSelectable.OnInteractHandler getClickHandler(int btn)
    {
        return delegate
        {
            Cards[btn].AddInteractionPunch();

            if (_isSolved)
            {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Cards[btn].transform);
                return false;
            }

            if (_selected.Contains(btn))
            {
                Audio.PlaySoundAtTransform("Deselect", Cards[btn].transform);
                SetSymbol(btn, false);
                _selected.Remove(btn);
            }
            else
            {
                _selected.Add(btn);
                if (_selected.Count == 3 && _selected.All(s => _solution.Cards.Contains(_displayedCards[s])))
                {
                    Debug.LogFormat("[S.E.T. #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    SetSymbol(btn, true);
                    _isSolved = true;
                    Audio.PlaySoundAtTransform("Chime", Cards[btn].transform);
                }
                else if (_selected.Count == 3)
                {
                    Debug.LogFormat("[S.E.T. #{0}] Incorrect set selected: {1}, {2}, {3}.", _moduleId, coords(_selected[0]), coords(_selected[1]), coords(_selected[2]));
                    Module.HandleStrike();
                    _selected.Remove(btn);
                }
                else
                {
                    SetSymbol(btn, true);
                    Audio.PlaySoundAtTransform("Stamp", Cards[btn].transform);
                }
            }
            return false;
        };
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"press a1 a2 [press any number of buttons in that order; a–c is columns and 1–3 is rows] | !{0} press tm br";
#pragma warning restore 414

    private KMSelectable[] ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var m = Regex.Match(command + " ", @"^\s*(?:press\s|submit\s|select\s|toggle\s|push\s)?\s*((?:([abc][123]|[lcrtmb][lcrtmb])\s+)+)\s*$");
        if (!m.Success)
            return null;
        return m.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(str =>
        {
            if (str[0] >= 'a' && str[0] <= 'c' && str[1] >= '1' && str[1] <= '3')
                return Cards[(str[0] - 'a') + 3 * (str[1] - '1')];
            var x = str.Contains('l') ? 0 : str.Contains('r') ? 2 : 1;
            var y = str.Contains('t') ? 0 : str.Contains('b') ? 2 : 1;
            return Cards[x + 3 * y];
        }).ToArray();
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (_selected.Count > 0)
        {
            Cards[_selected[0]].OnInteract();
            yield return new WaitForSeconds(.1f);
        }

        foreach (var card in _solution.Cards)
        {
            Cards[Array.IndexOf(_displayedCards, card)].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
    }
}
