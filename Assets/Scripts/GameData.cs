using Unity.Netcode;
using Unity.Collections;
using System;

public enum GameStateImpostor
{
    Playing,
    Voting,
    GameOver
}

public enum GameStateHigherOrLower
{
    Voting,
    Result,
}

public enum GameRole
{
    None,
    Trainer,
    TeamRocket
}

public enum GameType
{
    Impostor,
    HigherOrLower,
    Prox
}