import { useEffect, useCallback } from "react";
import { X, Download } from "lucide-react";

interface ImageLightboxProps {
  src: string;
  alt: string;
  fileName: string;
  onClose: () => void;
}

export function ImageLightbox({ src, alt, fileName, onClose }: ImageLightboxProps) {
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    },
    [onClose],
  );

  useEffect(() => {
    document.addEventListener("keydown", handleKeyDown);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "";
    };
  }, [handleKeyDown]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/85 backdrop-blur-sm"
      onClick={onClose}
    >
      <div className="absolute top-4 right-4 flex items-center gap-2 z-10">
        <a
          href={`${src}${src.includes("?") ? "&" : "?"}download=true`}
          download={fileName}
          onClick={(e) => e.stopPropagation()}
          className="p-2 rounded-full bg-gray-800/80 text-gray-300 hover:text-white hover:bg-gray-700 transition-colors"
          title="Download"
        >
          <Download size={20} />
        </a>
        <button
          onClick={onClose}
          className="p-2 rounded-full bg-gray-800/80 text-gray-300 hover:text-white hover:bg-gray-700 transition-colors"
          title="Close"
        >
          <X size={20} />
        </button>
      </div>

      <img
        src={src}
        alt={alt}
        onClick={(e) => e.stopPropagation()}
        className="max-w-[90vw] max-h-[90vh] object-contain rounded shadow-2xl"
      />

      <div
        className="absolute bottom-4 left-1/2 -translate-x-1/2 text-sm text-gray-400 bg-gray-900/70 px-3 py-1 rounded"
        onClick={(e) => e.stopPropagation()}
      >
        {fileName}
      </div>
    </div>
  );
}
