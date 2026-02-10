#pragma once
#include <atomic>

namespace ArisenEngine::Containers
{
    /**
     * @brief A simple lock-free stack for cross-thread resource recycling.
     *        This is a MPMC (Multiple Producer, Multiple Consumer) stack, 
     *        though in our RHI case it's mostly MPSC.
     */
    template <typename T>
    class LockFreeStack
    {
    private:
        struct Node
        {
            T data;
            Node* next;
        };

        std::atomic<Node*> m_Head{nullptr};

    public:
        LockFreeStack() = default;
        ~LockFreeStack()
        {
            T value;
            while (TryPop(value)) { }
        }

        NO_COPY_NO_MOVE(LockFreeStack)

        /**
         * @brief Push an item onto the stack.
         */
        void Push(T value)
        {
            Node* newNode = new Node{value, nullptr};
            newNode->next = m_Head.load(std::memory_order_relaxed);
            while (!m_Head.compare_exchange_weak(newNode->next, newNode, 
                                                std::memory_order_release, 
                                                std::memory_order_relaxed))
            {
                // Retry until successful
            }
        }

        /**
         * @brief Attempt to pop an item from the stack.
         */
        bool TryPop(T& outValue)
        {
            Node* oldHead = m_Head.load(std::memory_order_acquire);
            while (oldHead && !m_Head.compare_exchange_weak(oldHead, oldHead->next, 
                                                           std::memory_order_release, 
                                                           std::memory_order_relaxed))
            {
                // Retry until successful or empty
            }

            if (oldHead)
            {
                outValue = oldHead->data;
                delete oldHead;
                return true;
            }
            return false;
        }

        /**
         * @brief Check if the stack is empty.
         */
        bool IsEmpty() const
        {
            return m_Head.load(std::memory_order_relaxed) == nullptr;
        }
    };
}
