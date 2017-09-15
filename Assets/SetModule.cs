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
    public GameObject CardTemplate;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        for (int i = 0; i < 12; i++)
        {
            var card = Instantiate(CardTemplate);
            card.GetComponent<MeshRenderer>().material.mainTexture = Symbols[i];
            card.transform.parent = CardTemplate.transform.parent;
            card.transform.localPosition = new Vector3(-.06f + .04f * (i % 4), .01501f, .02f - .04f * (i / 4));
            card.transform.localEulerAngles = new Vector3(90, 0, 0);
            card.transform.localScale = new Vector3(.04f, .04f, .04f);
            card.name = string.Format("Card {0}{1}", (char) ('A' + (i % 4)), (char) ('1' + (i / 4)));
        }
        CardTemplate.SetActive(false);
    }
}
