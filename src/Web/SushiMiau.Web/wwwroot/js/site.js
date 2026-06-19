document.addEventListener("click", (event) => {
  const openButton = event.target.closest("[data-dialog-open]");
  if (openButton) {
    const dialog = document.querySelector(openButton.dataset.dialogOpen);
    if (dialog && typeof dialog.showModal === "function") {
      dialog.showModal();
    }
  }

  const closeButton = event.target.closest("[data-dialog-close]");
  if (closeButton) {
    closeButton.closest("dialog")?.close();
  }

  const confirmButton = event.target.closest("[data-confirm]");
  if (confirmButton && !window.confirm(confirmButton.dataset.confirm || "Esta seguro?")) {
    event.preventDefault();
  }
});

document.addEventListener("click", (event) => {
  const removeButton = event.target.closest("[data-remove-order-line]");
  if (!removeButton) {
    return;
  }

  const row = removeButton.closest("[data-line-row]");
  const container = removeButton.closest("[data-lines-container]");
  if (!row || !container) {
    return;
  }

  const rows = container.querySelectorAll("[data-line-row]");
  if (rows.length === 1) {
    const select = row.querySelector("[data-menu-select]");
    const quantity = row.querySelector("[data-line-quantity]");
    const price = row.querySelector("[data-line-price]");
    if (select) select.value = "";
    if (quantity) quantity.value = "1";
    if (price) price.value = "0.00";
  } else {
    row.remove();
  }

  refreshLineNames(container);
  const form = container.closest("[data-order-form]");
  if (form) {
    updateOrderTotals(form);
  }
});

const syncTableCapacity = (select) => {
  const form = select.closest("form");
  const partySize = form?.querySelector("[data-party-size]");
  const help = form?.querySelector("[data-capacity-help]");
  const capacity = Number(select.selectedOptions[0]?.dataset.capacity || 0);
  if (!partySize) {
    return;
  }

  if (capacity > 0) {
    partySize.max = String(capacity);
    partySize.setCustomValidity(Number(partySize.value) > capacity
      ? `Esta mesa admite como maximo ${capacity} personas.`
      : "");
    if (help) help.textContent = `Capacidad maxima: ${capacity} personas.`;
  } else {
    partySize.removeAttribute("max");
    partySize.setCustomValidity("");
    if (help) help.textContent = "Seleccione una mesa para ver su capacidad.";
  }
};

document.querySelectorAll("[data-table-capacity-select]").forEach(syncTableCapacity);

document.addEventListener("change", (event) => {
  const tableSelect = event.target.closest("[data-table-capacity-select]");
  if (tableSelect) {
    syncTableCapacity(tableSelect);
  }
});

document.addEventListener("input", (event) => {
  const partySize = event.target.closest("[data-party-size]");
  if (partySize) {
    const tableSelect = partySize.closest("form")?.querySelector("[data-table-capacity-select]");
    if (tableSelect) syncTableCapacity(tableSelect);
  }
});

document.addEventListener("submit", (event) => {
  const form = event.target;
  if (form.matches("[data-confirm]") && !window.confirm(form.dataset.confirm || "Esta seguro?")) {
    event.preventDefault();
  }
});

const formatMoney = (value) => `Bs ${value.toFixed(2)}`;

const updateOrderTotals = (form) => {
  const rows = [...form.querySelectorAll("[data-line-row]")];
  const subtotal = rows.reduce((sum, row) => {
    const fields = row.querySelectorAll("input");
    const quantity = Number(row.querySelector("[data-line-quantity]")?.value || fields[1]?.value || 0);
    const price = Number(row.querySelector("[data-line-price]")?.value || fields[2]?.value || 0);
    const rowTotal = quantity * price;

    const totalInput = row.querySelector("[data-line-total]");
    if (totalInput) {
      totalInput.value = rowTotal.toFixed(2);
    }

    return sum + rowTotal;
  }, 0);
  const tax = Math.round(subtotal * 13) / 100;
  const deliveryFee = Number(form.querySelector("[data-delivery-fee]")?.value || 0);
  const total = subtotal + tax + deliveryFee;

  form.querySelector("[data-order-subtotal]").textContent = formatMoney(subtotal);
  form.querySelector("[data-order-tax]").textContent = formatMoney(tax);
  form.querySelector("[data-order-total]").textContent = formatMoney(total);
};

const syncLinePrice = (select) => {
  const row = select.closest("[data-line-row]");
  const price = select.selectedOptions[0]?.dataset.price || "0.00";
  const priceInput = row?.querySelector("[data-line-price]");
  if (priceInput) {
    priceInput.value = Number(price).toFixed(2);
  }
};

const refreshLineNames = (container) => {
  const prefix = container.dataset.linePrefix;
  container.querySelectorAll("[data-line-row]").forEach((row, index) => {
    const select = row.querySelector("[data-menu-select]");
    const quantity = row.querySelector("[data-line-quantity]");
    const price = row.querySelector("[data-line-price]");
    if (select) select.name = `${prefix}.Lines[${index}].ItemName`;
    if (quantity) quantity.name = `${prefix}.Lines[${index}].Quantity`;
    if (price) price.name = `${prefix}.Lines[${index}].UnitPrice`;
  });
};

document.querySelectorAll("[data-lines-container]").forEach((container) => refreshLineNames(container));
document.querySelectorAll("[data-order-form]").forEach((form) => updateOrderTotals(form));

document.addEventListener("input", (event) => {
  const form = event.target.closest("[data-order-form]");
  if (form) {
    updateOrderTotals(form);
  }
});

document.addEventListener("change", (event) => {
  const select = event.target.closest("[data-menu-select]");
  if (select) {
    syncLinePrice(select);
    const form = select.closest("[data-order-form]");
    if (form) {
      updateOrderTotals(form);
    }
  }
});

document.addEventListener("click", (event) => {
  const addButton = event.target.closest("[data-add-order-line]");
  if (!addButton) {
    return;
  }

  const container = addButton.closest("[data-lines-container]");
  const template = container?.querySelector("[data-line-template]");
  if (!container || !template) {
    return;
  }

  const row = template.content.firstElementChild.cloneNode(true);
  container.insertBefore(row, addButton);
  refreshLineNames(container);
  const select = row.querySelector("[data-menu-select]");
  if (select) {
    syncLinePrice(select);
  }
  const form = addButton.closest("[data-order-form]");
  if (form) {
    updateOrderTotals(form);
  }
});

const refreshPurchaseLineNames = (container) => {
  container.querySelectorAll("[data-purchase-line]").forEach((row, index) => {
    const fields = row.querySelectorAll("select, input");
    if (fields[0]) fields[0].name = `PurchaseOrder.Lines[${index}].InventoryItemId`;
    if (fields[1]) fields[1].name = `PurchaseOrder.Lines[${index}].Quantity`;
    if (fields[2]) fields[2].name = `PurchaseOrder.Lines[${index}].UnitPrice`;
  });
};

document.querySelectorAll("[data-purchase-lines]").forEach(refreshPurchaseLineNames);

document.addEventListener("click", (event) => {
  const addButton = event.target.closest("[data-add-purchase-line]");
  if (!addButton) {
    return;
  }

  const container = addButton.closest("[data-purchase-lines]");
  const template = container?.querySelector("[data-purchase-template]");
  if (!container || !template) {
    return;
  }

  container.insertBefore(template.content.firstElementChild.cloneNode(true), addButton);
  refreshPurchaseLineNames(container);
});

const activateTableDetail = (button) => {
  const target = document.querySelector(button.dataset.tableTarget);
  if (!target) {
    return;
  }

  document.querySelectorAll("[data-table-target]").forEach((item) => item.classList.remove("active"));
  document.querySelectorAll("[data-table-detail]").forEach((item) => item.classList.remove("active"));
  button.classList.add("active");
  target.classList.add("active");
};

const firstTable = document.querySelector("[data-table-target]");
if (firstTable) {
  activateTableDetail(firstTable);
}

document.addEventListener("click", (event) => {
  const tableButton = event.target.closest("[data-table-target]");
  if (tableButton) {
    activateTableDetail(tableButton);
  }
});

document.addEventListener("click", (event) => {
  const clearButton = event.target.closest("[data-clear-order-line]");
  if (!clearButton) {
    return;
  }

  const row = clearButton.closest("[data-line-row]");
  if (!row) {
    return;
  }

  const nameInput = row.querySelector("input[placeholder='Producto']");
  const qtyInput = row.querySelector("[data-line-quantity]");
  const priceInput = row.querySelector("[data-line-price]");
  const totalInput = row.querySelector("[data-line-total]");

  if (nameInput) nameInput.value = "";
  if (qtyInput) qtyInput.value = "0";
  if (priceInput) priceInput.value = "0.00";
  if (totalInput) totalInput.value = "0.00";

  const form = row.closest("[data-order-form]");
  if (form) {
    updateOrderTotals(form);
  }
});

const syncInputLinePrice = (input) => {
  const row = input.closest("[data-line-row]");
  if (!row) return;

  const priceInput = row.querySelector("[data-line-price]");
  if (!priceInput) return;

  const datalist = document.getElementById("menuItems");
  if (!datalist) return;

  const option = [...datalist.options].find(opt => opt.value.toLowerCase() === input.value.trim().toLowerCase());
  const price = option?.dataset.price || "0.00";
  priceInput.value = Number(price).toFixed(2);
};

document.addEventListener("input", (event) => {
  const input = event.target.closest("input[list='menuItems']");
  if (input) {
    syncInputLinePrice(input);
    const form = input.closest("[data-order-form]");
    if (form) {
      updateOrderTotals(form);
    }
  }
});
