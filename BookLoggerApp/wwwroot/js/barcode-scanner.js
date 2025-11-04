// Barcode Scanner using Quagga.js for ISBN scanning
window.barcodeScanner = {
    dotNetHelper: null,
    isScanning: false,
    videoElement: null,
    stream: null,

    // Initialize the scanner
    async initialize(dotNetHelper, videoElementId) {
        this.dotNetHelper = dotNetHelper;
        this.videoElement = document.getElementById(videoElementId);

        if (!this.videoElement) {
            console.error('Video element not found:', videoElementId);
            return { success: false, error: 'Video element not found' };
        }

        try {
            // Request camera access
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: 'environment', // Use back camera on mobile
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                }
            });

            this.videoElement.srcObject = this.stream;
            await this.videoElement.play();

            this.isScanning = true;
            this.startScanning();

            return { success: true };
        } catch (error) {
            console.error('Camera access error:', error);
            return {
                success: false,
                error: error.message || 'Failed to access camera'
            };
        }
    },

    // Start scanning for barcodes
    startScanning() {
        if (!Quagga) {
            console.error('Quagga.js not loaded');
            return;
        }

        Quagga.init({
            inputStream: {
                name: "Live",
                type: "LiveStream",
                target: this.videoElement,
                constraints: {
                    facingMode: "environment"
                }
            },
            decoder: {
                readers: [
                    "ean_reader",      // EAN-13 (ISBN-13)
                    "ean_8_reader",    // EAN-8
                    "upc_reader",      // UPC-A
                    "upc_e_reader"     // UPC-E
                ]
            },
            locate: true,
            locator: {
                patchSize: "medium",
                halfSample: true
            },
            numOfWorkers: 2,
            frequency: 10
        }, (err) => {
            if (err) {
                console.error('Quagga initialization error:', err);
                this.dotNetHelper.invokeMethodAsync('OnScanError', err.message);
                return;
            }
            Quagga.start();
        });

        // Listen for detected barcodes
        Quagga.onDetected((result) => {
            if (result && result.codeResult && result.codeResult.code) {
                const code = result.codeResult.code;
                console.log('Barcode detected:', code);

                // Validate ISBN format (10 or 13 digits)
                if (this.isValidIsbn(code)) {
                    // Stop scanning after successful detection
                    this.stopScanning();
                    // Notify Blazor component
                    this.dotNetHelper.invokeMethodAsync('OnBarcodeDetected', code);
                }
            }
        });
    },

    // Validate ISBN format
    isValidIsbn(code) {
        const cleaned = code.replace(/[-\s]/g, '');
        return cleaned.length === 10 || cleaned.length === 13;
    },

    // Stop scanning and release camera
    stopScanning() {
        this.isScanning = false;

        if (Quagga) {
            Quagga.stop();
        }

        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }

        if (this.videoElement) {
            this.videoElement.srcObject = null;
        }
    },

    // Toggle torch/flashlight (if supported)
    async toggleTorch(enable) {
        try {
            if (this.stream) {
                const track = this.stream.getVideoTracks()[0];
                const capabilities = track.getCapabilities();

                if ('torch' in capabilities) {
                    await track.applyConstraints({
                        advanced: [{ torch: enable }]
                    });
                    return { success: true };
                } else {
                    return { success: false, error: 'Torch not supported' };
                }
            }
            return { success: false, error: 'No active stream' };
        } catch (error) {
            return { success: false, error: error.message };
        }
    }
};
