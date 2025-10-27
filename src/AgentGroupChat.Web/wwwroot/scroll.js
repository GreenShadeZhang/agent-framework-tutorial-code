// 立即滚动到底部（使用 requestAnimationFrame 优化性能）
window.scrollToBottom = function (elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        requestAnimationFrame(() => {
            element.scrollTop = element.scrollHeight;
        });
    }
};

// 平滑滚动到底部（带动画效果）
window.smoothScrollToBottom = function (elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        requestAnimationFrame(() => {
            element.scrollTo({
                top: element.scrollHeight,
                behavior: 'smooth'
            });
        });
    }
};
