#pragma once

class GameInstance
{
public:
    ~GameInstance();
    
    void Initialize();
    void Loop();

    void OnKeyDown(char KeyCode);
    void OnKeyUp(char KeyCode);
};
