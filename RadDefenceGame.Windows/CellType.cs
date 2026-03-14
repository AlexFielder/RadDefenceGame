namespace RadDefenceGame.Windows;

public enum CellType
{
    Empty,
    Wall,       // blocks path; towers can be built here
    Tower,      // wall with a weapon on it
    Spawn,
    Exit
}
