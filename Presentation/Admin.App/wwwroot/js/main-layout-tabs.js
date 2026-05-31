(function () {
    function getOverflowTabIndexes() {
        const nav = document.querySelector('.MainTab > .ant-tabs-nav');
        const ops = document.querySelector('.MainTab > .ant-tabs-nav .ant-tabs-nav-operations');
        const tabs = Array.from(document.querySelectorAll('.MainTab > .ant-tabs-nav .ant-tabs-tab'));

        if (!nav || !ops || tabs.length === 0) {
            return [];
        }

        const navRect = nav.getBoundingClientRect();
        const opsRect = ops.getBoundingClientRect();
        const visibleRight = opsRect.left;

        return tabs
            .map((tab, index) => ({ tab, index }))
            .filter(item => {
                const rect = item.tab.getBoundingClientRect();
                return rect.left < navRect.left || rect.right > visibleRight;
            })
            .map(item => item.index);
    }

    window.netEngineMainLayout = {
        getOverflowTabIndexes
    };
})();