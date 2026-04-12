import React, { Component } from "react";
import './ToggleSwitch.scss';


const ToggleSwitch = ({
  id,
  name,
  checked,
  onChange,
  optionLabels,
  small,
  disabled
}) => {

  function handleKeyPress(e) {
    if (e.keyCode !== 32) return;
    e.preventDefault();
    onChange(!checked);
  }

  return (
    <div className={"toggle-switch" + (small ? " small-switch" : "")} >

      <input
        type="checkbox"
        name={name}
        className="toggle-switch-checkbox"
        id={id}
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        disabled={disabled}
      />
      {/* <input type="checkbox" class="toggle-switch-checkbox" name="toggleSwitch" id="toggleSwitch" /> */}

      {id ? (
        <label
          className="toggle-switch-label"
          tabIndex={disabled ? -1 : 1}
          onKeyDown={(e) => handleKeyPress(e)}
          htmlFor={id}
        >
          <span
            className={
              disabled
                ? "toggle-switch-inner toggle-switch-disabled"
                : "toggle-switch-inner"
            }
            data-yes={""}
            data-no={""}
            tabIndex={-1}
          />
          <span
            className={
              disabled
                ? "toggle-switch-switch toggle-switch-disabled"
                : "toggle-switch-switch"
            }
            tabIndex={-1}
          />
        </label>
      ) : (
        <label>
          B
        </label>
      )}

    </div>

  );
};

ToggleSwitch.defaultProps = {
  optionLabels: ["Yes", "No"]
};

export default ToggleSwitch;