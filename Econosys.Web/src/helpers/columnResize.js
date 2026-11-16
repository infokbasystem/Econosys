export const MIN_COLUMN_WIDTH = 64;

export const resizeColumn = (widths, index, width) => widths.map((currentWidth, columnIndex) => (
    columnIndex === index ? Math.max(MIN_COLUMN_WIDTH, width) : currentWidth
));
