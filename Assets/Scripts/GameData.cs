using Unity.Netcode;
using Unity.Collections;
using System;

public enum GameState
{
    Playing,
    Voting,
    GameOver
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
    SayOnePokemon,
    Quiz
}