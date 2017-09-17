using System;
using System.Collections.Generic;
using System.Linq;
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

    public Texture[] Symbols;
    public KMSelectable[] Cards;
    public KMSelectable MainSelectable;

    private MeshRenderer[] _cardImages;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    struct SetCard : IEquatable<SetCard>
    {
        public int Index { get; private set; }
        public int X { get { return Index % 3; } }
        public int Y { get { return (Index / 3) % 3; } }
        public int NumDots { get { return (Index / 9) % 3; } }
        public int Filling { get { return Index / 27; } }

        public SetCard(int x, int y, int numDots, int filling) { Index = x + 3 * (y + 3 * (numDots + 3 * filling)); }
        public SetCard(int index) { Index = index; }

        private static int get3rdValue(int one, int two) { return (6 - one - two) % 3; }
        public SetCard Get3rdInSet(SetCard other) { return new SetCard(get3rdValue(X, other.X), get3rdValue(Y, other.Y), get3rdValue(NumDots, other.NumDots), get3rdValue(Filling, other.Filling)); }
        public static SetCard GetRandom() { return new SetCard(Rnd.Range(0, 81)); }
        public bool Equals(SetCard other) { return other.Index == Index; }
        public override int GetHashCode() { return Index; }
        public override bool Equals(object obj) { return obj is SetCard && Equals((SetCard) obj); }
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

        // The textures are in random order and I couldn’t be bothered to fix it in Unity.
        Symbols = Enumerable.Range(0, 81).Select(ix => Symbols.FirstOrDefault(sy => sy.name == string.Format("Icon{0}{1}{2}{3}", (char) ('A' + (ix % 3)), (char) ('1' + ((ix / 3) % 3)), (char) ('a' + ((ix / 9) % 3)), (char) ('1' + ((ix / 27) % 3))))).ToArray();

        _cardImages = new MeshRenderer[Cards.Length];
        for (int i = 0; i < Cards.Length; i++)
            _cardImages[i] = Cards[i].transform.FindChild("CardImage").GetComponent<MeshRenderer>();

        tryAgain:
        var solution = SetSet.GetRandom(avoidSameSymbol: true);
        var cards = new HashSet<SetCard> { solution.One, solution.Two, solution.Three };
        var iter = 0;
        while (cards.Count < 12)
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

        var cardsOrdered = cards.ToList().Shuffle();
        for (int i = 0; i < cardsOrdered.Count; i++)
            _cardImages[i].material.mainTexture = Symbols[cardsOrdered[i].Index];

        Debug.LogFormat("Solution: {0}, {1}, {2}", cardsOrdered.IndexOf(solution.One), cardsOrdered.IndexOf(solution.Two), cardsOrdered.IndexOf(solution.Three));
    }

    private static IEnumerable<int> GetRandomDimensionValues()
    {
        return (Rnd.Range(0, 2) == 0 ? Enumerable.Repeat(Rnd.Range(0, 3), 3) : Enumerable.Range(0, 3).ToList().Shuffle());
    }
}
