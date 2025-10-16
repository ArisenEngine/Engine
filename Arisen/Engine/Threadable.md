# 线程时序图示例（VSCode Mermaid）


sequenceDiagram
    autonumber

    participant MainThread as 主线程
    participant WorkerThread as 工作线程
    participant RenderThread as 渲染线程

    Note over MainThread: 启动流程

    MainThread->>WorkerThread: 创建并启动后台任务
    WorkerThread-->>MainThread: 任务已启动

    par 并行阶段
        WorkerThread->>WorkerThread: 执行耗时计算
        RenderThread->>RenderThread: 渲染上一帧
    and
        RenderThread-->>MainThread: 渲染完成
        WorkerThread-->>MainThread: 计算完成
    end

    MainThread->>MainThread: 合并结果 & 准备下一帧
    Note right of MainThread: 进入下一帧循环
