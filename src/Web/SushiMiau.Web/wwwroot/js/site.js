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
