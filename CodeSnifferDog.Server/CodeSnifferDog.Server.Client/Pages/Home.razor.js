const dropzoneSelector = "#new-project-dropzone";
const inputId = "new-project-zip-input";

window.codeSnifferDogProjectUpload = {
    async upload(inputId, url) {
        const uploadInput = document.getElementById(inputId);
        const file = uploadInput?.files?.[0];

        if (!file) {
            return {
                success: false,
                message: "Choose a .zip archive before uploading.",
            };
        }

        const formData = new FormData();
        formData.append("file", file, file.name);

        const response = await fetch(url, {
            method: "POST",
            body: formData,
        });

        const responseText = await response.text();
        let payload = null;

        if (responseText) {
            try {
                payload = JSON.parse(responseText);
            } catch {
                payload = null;
            }
        }

        if (!response.ok) {
            return {
                success: false,
                message: payload?.message ?? `Upload failed with status ${response.status} (${response.statusText}).`,
            };
        }

        return {
            success: true,
            projectId: payload?.projectId,
            originalFileName: payload?.originalFileName,
        };
    },
};

document.addEventListener("dragover", (event) => {
    if (hasDraggedFiles(event)) {
        event.preventDefault();
    }

    const dropzone = event.target?.closest?.(dropzoneSelector);
    if (dropzone) {
        dropzone.classList.add("drag-active");
    }
});

document.addEventListener("dragenter", (event) => {
    const dropzone = event.target?.closest?.(dropzoneSelector);
    if (!dropzone) {
        return;
    }

    event.preventDefault();
    dropzone.classList.add("drag-active");
});

document.addEventListener("dragleave", (event) => {
    const dropzone = event.target?.closest?.(dropzoneSelector);
    if (!dropzone || dropzone.contains(event.relatedTarget)) {
        return;
    }

    event.preventDefault();
    dropzone.classList.remove("drag-active");
});

document.addEventListener("dragend", () => {
    document.querySelector(dropzoneSelector)?.classList.remove("drag-active");
});

document.addEventListener("drop", (event) => {
    if (hasDraggedFiles(event)) {
        event.preventDefault();
    }

    const dropzone = event.target?.closest?.(dropzoneSelector);
    document.querySelector(dropzoneSelector)?.classList.remove("drag-active");

    if (!dropzone) {
        return;
    }

    const files = event.dataTransfer?.files;
    const input = document.getElementById(inputId);
    if (!files || files.length === 0 || !input) {
        return;
    }

    const transfer = new DataTransfer();
    transfer.items.add(files[0]);
    input.files = transfer.files;
    input.dispatchEvent(new Event("change", { bubbles: true }));
});

function hasDraggedFiles(event) {
    return Array.from(event.dataTransfer?.types ?? []).includes("Files");
}
