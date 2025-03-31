function GetSpanDateByIndex(i) {
    const xpath = `(//td[contains(@class, 'xW') and contains(@class, 'xY')]/span/span)[${i}]`;
    const element = document.evaluate(xpath, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;

    return element ? element.textContent : `Element at index ${i} not found.`;
}
