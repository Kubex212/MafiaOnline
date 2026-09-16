import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';

type ToastKind = 'success' | 'error' | 'info' | 'warning';
type ToastItem = { id: number; message: string; kind: ToastKind };
type ConfirmOptions = { title: string; message: string; confirmLabel: string; cancelLabel: string; danger?: boolean };
type PromptOptions = ConfirmOptions & { label: string; placeholder?: string; error?: string };
type DialogState =
    | ({ mode: 'confirm'; resolve: (value: boolean) => void } & ConfirmOptions)
    | ({ mode: 'prompt'; resolve: (value: string | null) => void } & PromptOptions);

type UiFeedbackValue = {
    toast: (message: string, kind?: ToastKind) => void;
    confirm: (options: ConfirmOptions) => Promise<boolean>;
    prompt: (options: PromptOptions) => Promise<string | null>;
};

const UiFeedbackContext = createContext<UiFeedbackValue | null>(null);

export function UiFeedbackProvider({ children }: { children: React.ReactNode }) {
    const [toasts, setToasts] = useState<ToastItem[]>([]);
    const [dialog, setDialog] = useState<DialogState | null>(null);
    const [value, setValue] = useState('');
    const inputRef = useRef<HTMLInputElement | null>(null);
    const nextToastId = useRef(1);

    const dismissToast = useCallback((id: number) => {
        setToasts(items => items.filter(item => item.id !== id));
    }, []);

    const toast = useCallback((message: string, kind: ToastKind = 'info') => {
        if (!message) return;
        const id = nextToastId.current++;
        setToasts(items => [...items.slice(-3), { id, message, kind }]);
        window.setTimeout(() => dismissToast(id), kind === 'error' ? 6000 : 3800);
    }, [dismissToast]);

    const confirm = useCallback((options: ConfirmOptions) => new Promise<boolean>(resolve => {
        setValue('');
        setDialog({ ...options, mode: 'confirm', resolve });
    }), []);

    const prompt = useCallback((options: PromptOptions) => new Promise<string | null>(resolve => {
        setValue('');
        setDialog({ ...options, mode: 'prompt', resolve });
    }), []);

    const closeDialog = useCallback((accepted: boolean) => {
        setDialog(current => {
            if (!current) return null;
            if (current.mode === 'confirm') current.resolve(accepted);
            else current.resolve(accepted ? value : null);
            return null;
        });
    }, [value]);

    useEffect(() => {
        if (!dialog) return;
        const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        const oldOverflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        const timer = window.setTimeout(() => inputRef.current?.focus(), 0);
        const onKeyDown = (event: KeyboardEvent) => {
            if (event.key === 'Escape') closeDialog(false);
        };
        window.addEventListener('keydown', onKeyDown);
        return () => {
            window.clearTimeout(timer);
            window.removeEventListener('keydown', onKeyDown);
            document.body.style.overflow = oldOverflow;
            previous?.focus();
        };
    }, [dialog, closeDialog]);

    return (
        <UiFeedbackContext.Provider value={{ toast, confirm, prompt }}>
            {children}
            <div className="toast-region" aria-live="polite" aria-atomic="true">
                {toasts.map(item => (
                    <div key={item.id} className={`toast toast--${item.kind}`} role={item.kind === 'error' ? 'alert' : 'status'}>
                        <span className="toast__icon" aria-hidden>{item.kind === 'success' ? '✓' : item.kind === 'error' ? '!' : item.kind === 'warning' ? '⚠' : 'i'}</span>
                        <span className="toast__message">{item.message}</span>
                        <button className="toast__close" onClick={() => dismissToast(item.id)} aria-label="Dismiss notification">×</button>
                    </div>
                ))}
            </div>
            {dialog && (
                <div className="modal__backdrop" role="presentation" onMouseDown={event => {
                    if (event.target === event.currentTarget) closeDialog(false);
                }}>
                    <form className="modal confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="feedback-dialog-title"
                        onSubmit={event => { event.preventDefault(); closeDialog(true); }}>
                        <header className="modal__header">
                            <h2 className="modal__title" id="feedback-dialog-title">{dialog.title}</h2>
                            <button type="button" className="modal__close" onClick={() => closeDialog(false)} aria-label={dialog.cancelLabel}>✕</button>
                        </header>
                        <p className="confirm-dialog__message">{dialog.message}</p>
                        {dialog.mode === 'prompt' && (
                            <label className="field">
                                <span className="field__label">{dialog.label}</span>
                                <input ref={inputRef} className={`input ${dialog.error ? 'input--error' : ''}`} type="password"
                                    autoComplete="current-password" value={value} placeholder={dialog.placeholder}
                                    onChange={event => setValue(event.target.value)} />
                                {dialog.error && <span className="field__error" role="alert">{dialog.error}</span>}
                            </label>
                        )}
                        <footer className="modal__actions">
                            <button type="button" className="btn btn--ghost" onClick={() => closeDialog(false)}>{dialog.cancelLabel}</button>
                            <button type="submit" className={`btn ${dialog.danger ? 'btn--danger' : 'btn--primary'}`}
                                disabled={dialog.mode === 'prompt' && !value.trim()}>
                                {dialog.confirmLabel}
                            </button>
                        </footer>
                    </form>
                </div>
            )}
        </UiFeedbackContext.Provider>
    );
}

export function useUiFeedback() {
    const value = useContext(UiFeedbackContext);
    if (!value) throw new Error('useUiFeedback must be used inside UiFeedbackProvider');
    return value;
}