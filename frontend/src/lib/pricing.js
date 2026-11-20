export function hasDiscount(price, listPrice) {
  return Number.isFinite(price) && price >= 0 && Number.isFinite(listPrice) && listPrice > price;
}
