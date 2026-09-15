interface AuthFieldProps {
  id: string;
  label: string;
  type: string;
  autoComplete: string;
  minLength?: number;
  value: string;
  onChange: (value: string) => void;
}

// One labeled input, shared by Login/Register's email, password, and name fields.
export function AuthField({ id, label, type, autoComplete, minLength, value, onChange }: AuthFieldProps) {
  return (
    <div className="mb-3">
      <label htmlFor={id} className="form-label">
        {label}
      </label>
      <input
        id={id}
        type={type}
        className="form-control"
        autoComplete={autoComplete}
        required
        minLength={minLength}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}
