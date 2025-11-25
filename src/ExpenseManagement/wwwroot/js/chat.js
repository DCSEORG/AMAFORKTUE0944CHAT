// Chat Widget JavaScript

let chatHistory = [];

function toggleChat() {
    const container = document.getElementById('chat-container');
    container.classList.toggle('hidden');
    if (!container.classList.contains('hidden')) {
        document.getElementById('chat-input').focus();
    }
}

function handleKeyPress(event) {
    if (event.key === 'Enter') {
        sendMessage();
    }
}

async function sendMessage() {
    const input = document.getElementById('chat-input');
    const message = input.value.trim();
    
    if (!message) return;
    
    // Add user message to chat
    addMessage(message, 'user');
    input.value = '';
    
    // Add to history
    chatHistory.push({ role: 'user', content: message });
    
    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                message: message,
                history: chatHistory.slice(-10) // Keep last 10 messages for context
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            addMessage(data.message, 'assistant');
            chatHistory.push({ role: 'assistant', content: data.message });
        } else {
            addMessage('Sorry, I encountered an error. Please try again.', 'assistant');
        }
    } catch (error) {
        console.error('Chat error:', error);
        addMessage('Sorry, I couldn\'t connect to the server. Please try again.', 'assistant');
    }
}

function addMessage(text, role) {
    const messagesContainer = document.getElementById('chat-messages');
    const messageDiv = document.createElement('div');
    messageDiv.className = `chat-message ${role}`;
    
    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';
    
    // Format the message content
    contentDiv.innerHTML = formatMessage(text);
    
    messageDiv.appendChild(contentDiv);
    messagesContainer.appendChild(messageDiv);
    
    // Scroll to bottom
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

function formatMessage(text) {
    // Escape HTML first to prevent XSS
    let formatted = escapeHtml(text);
    
    // Apply formatting after escaping
    // Bold text: **text** -> <strong>text</strong>
    formatted = formatted.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    
    // Process lines for lists
    const lines = formatted.split('\n');
    let result = [];
    let inOrderedList = false;
    let inUnorderedList = false;
    
    for (let line of lines) {
        const trimmedLine = line.trim();
        
        // Ordered list item (1. item)
        const orderedMatch = trimmedLine.match(/^(\d+)\.\s+(.+)$/);
        if (orderedMatch) {
            if (!inOrderedList) {
                if (inUnorderedList) {
                    result.push('</ul>');
                    inUnorderedList = false;
                }
                result.push('<ol>');
                inOrderedList = true;
            }
            result.push(`<li>${orderedMatch[2]}</li>`);
            continue;
        }
        
        // Unordered list item (- item or * item)
        const unorderedMatch = trimmedLine.match(/^[-*]\s+(.+)$/);
        if (unorderedMatch) {
            if (!inUnorderedList) {
                if (inOrderedList) {
                    result.push('</ol>');
                    inOrderedList = false;
                }
                result.push('<ul>');
                inUnorderedList = true;
            }
            result.push(`<li>${unorderedMatch[1]}</li>`);
            continue;
        }
        
        // Regular line
        if (inOrderedList) {
            result.push('</ol>');
            inOrderedList = false;
        }
        if (inUnorderedList) {
            result.push('</ul>');
            inUnorderedList = false;
        }
        
        if (trimmedLine) {
            result.push(line);
        } else {
            result.push('<br>');
        }
    }
    
    // Close any open lists
    if (inOrderedList) result.push('</ol>');
    if (inUnorderedList) result.push('</ul>');
    
    return result.join('\n').replace(/\n(?!<)/g, '<br>');
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
