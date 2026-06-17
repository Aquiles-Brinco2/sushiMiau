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
    return sum + quantity * price;
  }, 0);
  const tax = Math.round(subtotal * 13) / 100;
  const total = subtotal + tax;

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
