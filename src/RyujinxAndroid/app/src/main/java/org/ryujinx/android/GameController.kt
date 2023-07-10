package org.ryujinx.android

import android.app.Activity
import android.content.Context
import android.view.KeyEvent
import android.view.LayoutInflater
import android.view.View
import android.widget.FrameLayout
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.GraphicsLayerScope
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleCoroutineScope
import androidx.lifecycle.flowWithLifecycle
import com.swordfish.radialgamepad.library.RadialGamePad
import com.swordfish.radialgamepad.library.config.ButtonConfig
import com.swordfish.radialgamepad.library.config.CrossConfig
import com.swordfish.radialgamepad.library.config.CrossContentDescription
import com.swordfish.radialgamepad.library.config.PrimaryDialConfig
import com.swordfish.radialgamepad.library.config.RadialGamePadConfig
import com.swordfish.radialgamepad.library.config.RadialGamePadTheme
import com.swordfish.radialgamepad.library.config.SecondaryDialConfig
import com.swordfish.radialgamepad.library.event.Event
import com.swordfish.radialgamepad.library.event.GestureType
import com.swordfish.radialgamepad.library.haptics.HapticConfig
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.flowOn
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.flow.onCompletion
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.shareIn
import kotlinx.coroutines.flow.subscribe
import kotlinx.coroutines.launch

typealias GamePad = RadialGamePad
typealias GamePadConfig = RadialGamePadConfig

class GameController(var activity: Activity, var ryujinxNative: RyujinxNative = RyujinxNative()) {
    var leftGamePad: GamePad
    var rightGamePad: GamePad
    var controllerId: Int = -1

    init {
        leftGamePad = GamePad(generateConfig(true), 16f, activity)
        rightGamePad = GamePad(generateConfig(false), 16f, activity)

        leftGamePad.primaryDialMaxSizeDp = 200f
        rightGamePad.primaryDialMaxSizeDp = 200f

        leftGamePad.gravityX = -1f
        leftGamePad.gravityY = 1f
        rightGamePad.gravityX = 1f
        rightGamePad.gravityY = 1f
    }

    @Composable
    fun Compose(lifecycleScope: LifecycleCoroutineScope, lifecycle:Lifecycle) : Unit
    {

        AndroidView(
            modifier = Modifier.fillMaxSize(), factory = { context -> Create(context)})

        lifecycleScope.apply {
            lifecycleScope.launch {
                var events = merge(leftGamePad.events(),rightGamePad.events())
                    .shareIn(lifecycleScope, SharingStarted.Lazily)

                events.safeCollect {
                    handleEvent(it)
                };
            }
        }
    }

    private  fun Create(context: Context) : View
    {
        var inflator = LayoutInflater.from(context);
        var view = inflator.inflate(R.layout.game_layout, null)
        view.findViewById<FrameLayout>(R.id.leftcontainer)!!.addView(leftGamePad);
        view.findViewById<FrameLayout>(R.id.rightcontainer)!!.addView(rightGamePad);

        return view as View
    }

    fun connect(){
        if(controllerId == -1)
            controllerId = ryujinxNative.inputConnectGamepad(0)
    }

    private fun handleEvent(ev: Event) {
        if(controllerId == -1)
            controllerId = ryujinxNative.inputConnectGamepad(0)

        controllerId?.apply {
            when (ev) {
                is Event.Button -> {
                    var action = ev.action
                    when (action) {
                        KeyEvent.ACTION_UP -> {
                            ryujinxNative.inputSetButtonReleased(ev.id, this)
                        }
                        KeyEvent.ACTION_DOWN -> {
                            ryujinxNative.inputSetButtonPressed(ev.id, this)
                        }
                    }
                }
                is Event.Direction -> {
                    var direction = ev.id

                    when(direction) {
                        GamePadButtonInputId.DpadUp.ordinal -> {
                            if (ev.xAxis > 0)
                            {
                                ryujinxNative.inputSetButtonPressed(GamePadButtonInputId.DpadRight.ordinal, this)
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadLeft.ordinal, this)
                            }
                            else if (ev.xAxis < 0)
                            {
                                ryujinxNative.inputSetButtonPressed(GamePadButtonInputId.DpadLeft.ordinal, this)
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadRight.ordinal, this)
                            }
                            else
                            {
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadLeft.ordinal, this)
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadRight.ordinal, this)
                            }
                            if (ev.yAxis < 0)
                            {
                                ryujinxNative.inputSetButtonPressed(GamePadButtonInputId.DpadUp.ordinal, this)
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadDown.ordinal, this)
                            }
                            else if (ev.yAxis > 0)
                            {
                                ryujinxNative.inputSetButtonPressed(GamePadButtonInputId.DpadDown.ordinal, this)
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadUp.ordinal, this)
                            }
                            else
                            {
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadDown.ordinal, this)
                                ryujinxNative.inputSetButtonReleased(GamePadButtonInputId.DpadUp.ordinal, this)
                            }
                        }

                        GamePadButtonInputId.LeftStick.ordinal -> {
                            ryujinxNative.inputSetStickAxis(1, ev.xAxis, -ev.yAxis ,this)
                        }
                        GamePadButtonInputId.RightStick.ordinal -> {
                            ryujinxNative.inputSetStickAxis(2, ev.xAxis, -ev.yAxis ,this)
                        }
                    }
                }
            }
        }
    }
}

suspend fun <T> Flow<T>.safeCollect(
    block: suspend (T) -> Unit
) {
    this.catch {}
        .collect {
            block(it)
        }
}

private fun generateConfig(isLeft: Boolean): GamePadConfig {
    var distance = 0.05f

    if (isLeft) {
        return GamePadConfig(
            12,
            PrimaryDialConfig.Stick(
                GamePadButtonInputId.LeftStick.ordinal,
                GamePadButtonInputId.LeftStickButton.ordinal,
                setOf(),
                "LeftStick",
                null
            ),
            listOf(
                SecondaryDialConfig.Cross(
                    9,
                    3,
                    1.8f,
                    distance,
                    CrossConfig(
                        GamePadButtonInputId.DpadUp.ordinal,
                        CrossConfig.Shape.STANDARD,
                        null,
                        setOf(),
                        CrossContentDescription(),
                        true,
                        null
                    ),
                    SecondaryDialConfig.RotationProcessor()
                ),
                SecondaryDialConfig.SingleButton(
                    0,
                    1f,
                    0.05f,
                    ButtonConfig(
                        GamePadButtonInputId.Minus.ordinal,
                        "-",
                        true,
                        null,
                        "Minus",
                        setOf(),
                        true,
                        null
                    ),
                    null,
                    SecondaryDialConfig.RotationProcessor()
                ),
                SecondaryDialConfig.DoubleButton(
                    2,
                    0.05f,
                    ButtonConfig(
                        GamePadButtonInputId.LeftShoulder.ordinal,
                        "L",
                        true,
                        null,
                        "LeftBumper",
                        setOf(),
                        true,
                        null
                    ),
                    null,
                    SecondaryDialConfig.RotationProcessor()
                ),
                SecondaryDialConfig.SingleButton(
                    8,
                    1f,
                    0.05f,
                    ButtonConfig(
                        GamePadButtonInputId.LeftTrigger.ordinal,
                        "ZL",
                        true,
                        null,
                        "LeftTrigger",
                        setOf(),
                        true,
                        null
                    ),
                    null,
                    SecondaryDialConfig.RotationProcessor()
                ),
            )
        )
    } else {
        return GamePadConfig(
            12,
            PrimaryDialConfig.PrimaryButtons(
                listOf(
                    ButtonConfig(
                        GamePadButtonInputId.A.ordinal,
                        "A",
                        true,
                        null,
                        "A",
                        setOf(),
                        true,
                        null
                    ),
                    ButtonConfig(
                        GamePadButtonInputId.X.ordinal,
                        "X",
                        true,
                        null,
                        "X",
                        setOf(),
                        true,
                        null
                    ),
                    ButtonConfig(
                        GamePadButtonInputId.Y.ordinal,
                        "Y",
                        true,
                        null,
                        "Y",
                        setOf(),
                        true,
                        null
                    ),
                    ButtonConfig(
                        GamePadButtonInputId.B.ordinal,
                        "B",
                        true,
                        null,
                        "B",
                        setOf(),
                        true,
                        null
                    )
                ),
                null,
                0f,
                true,
                null
            ),
            listOf(
                SecondaryDialConfig.Stick(
                    7,
                    2,
                    3f,
                    0.05f,
                    GamePadButtonInputId.RightStick.ordinal,
                    GamePadButtonInputId.RightStickButton.ordinal,
                    null,
                    setOf(),
                    "RightStick",
                    SecondaryDialConfig.RotationProcessor()
                ),
                SecondaryDialConfig.SingleButton(
                    6,
                    1f,
                    0.05f,
                    ButtonConfig(
                        GamePadButtonInputId.Plus.ordinal,
                        "+",
                        true,
                        null,
                        "Plus",
                        setOf(),
                        true,
                        null
                    ),
                    null,
                    SecondaryDialConfig.RotationProcessor()
                ),
                SecondaryDialConfig.DoubleButton(
                    3,
                    0.05f,
                    ButtonConfig(
                        GamePadButtonInputId.RightShoulder.ordinal,
                        "R",
                        true,
                        null,
                        "RightBumper",
                        setOf(),
                        true,
                        null
                    ),
                    null,
                    SecondaryDialConfig.RotationProcessor()
                ),
                SecondaryDialConfig.SingleButton(
                    9,
                    1f,
                    0.05f,
                    ButtonConfig(
                        GamePadButtonInputId.RightTrigger.ordinal,
                        "ZR",
                        true,
                        null,
                        "RightTrigger",
                        setOf(),
                        true,
                        null
                    ),
                    null,
                    SecondaryDialConfig.RotationProcessor()
                )
            )
        )
    }
}